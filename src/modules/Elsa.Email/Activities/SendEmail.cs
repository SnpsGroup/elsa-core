using System.Collections;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Elsa.Email.Contracts;
using Elsa.Email.Models;
using Elsa.Email.Options;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.UIHints;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Elsa.Email.Activities;

/// <summary>
/// Send an email message.
/// </summary>
[Activity("Elsa", "Email", "Send an email message.", Kind = ActivityKind.Task)]
public class SendEmail : Activity
{
    /// <inheritdoc />
    public SendEmail([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <summary>
    /// The sender's email address.
    /// </summary>
    [Input(Description = "The sender's email address.")]
    public Input<string?> From { get; set; } = null!;

    /// <summary>
    /// The recipients email addresses.
    /// </summary>
    [Input(Description = "The recipients email addresses.", UIHint = InputUIHints.MultiText)]
    public Input<ICollection<string>> To { get; set; } = null!;

    /// <summary>
    /// The CC recipient email addresses.
    /// </summary>
    [Input(
        Description = "The CC recipient email addresses.",
        UIHint = InputUIHints.MultiText,
        Category = "More")]
    public Input<ICollection<string>> Cc { get; set; } = null!;

    /// <summary>
    /// The BCC recipients email addresses.
    /// </summary>
    [Input(
        Description = "The BCC recipients email addresses.",
        UIHint = InputUIHints.MultiText,
        Category = "More")]
    public Input<ICollection<string>> Bcc { get; set; } = null!;

    /// <summary>
    /// The subject of the email message.
    /// </summary>
    [Input(Description = "The subject of the email message.")]
    public Input<string?> Subject { get; set; } = null!;

    /// <summary>
    /// The attachments to send with the email message.
    /// </summary>
    [Input(
        Description = "The attachments to send with the email message. Can be (an array of) a fully-qualified file path, URL, stream, byte array or instances of EmailAttachment.",
        UIHint = InputUIHints.MultiLine
    )]
    public Input<object?> Attachments { get; set; } = null!;

    /// <summary>
    /// The body of the email message.
    /// </summary>
    [Input(
        Description = "The body of the email message.",
        UIHint = InputUIHints.MultiLine
    )]
    public Input<string> Body { get; set; } = null!;

    /// <summary>
    /// The activity to execute when an error occurs while trying to send the email.
    /// </summary>
    [Port] public IActivity? Error { get; set; }

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var message = new MimeMessage();
        var options = context.GetRequiredService<IOptions<SmtpOptions>>().Value;
        var from = string.IsNullOrWhiteSpace(From.GetOrDefault(context)) ? options.DefaultSender : From.Get(context)!;

        message.Sender = MailboxAddress.Parse(from);
        message.From.Add(MailboxAddress.Parse(from));
        message.Subject = Subject.GetOrDefault(context) ?? "";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = Body.GetOrDefault(context)
        };
        await AddAttachmentsAsync(context, bodyBuilder, cancellationToken);

        message.Body = bodyBuilder.ToMessageBody();

        SetRecipientsEmailAddresses(message.To, GetAddresses(context, To));
        SetRecipientsEmailAddresses(message.Cc, GetAddresses(context, Cc));
        SetRecipientsEmailAddresses(message.Bcc, GetAddresses(context, Bcc));

        var smtpService = context.GetRequiredService<ISmtpService>();
        var logger = context.GetRequiredService<ILogger<SendEmail>>();

        try
        {
            await smtpService.SendAsync(message, context.CancellationToken);
            await context.CompleteActivityAsync();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Error while sending email message");
            context.AddExecutionLogEntry("Error", e.Message, payload: new
            {
                e.StackTrace
            });
            await context.ScheduleActivityAsync(Error, OnErrorCompletedAsync);
        }
    }

    private async ValueTask OnErrorCompletedAsync(ActivityCompletedContext context) => await context.TargetContext.CompleteActivityAsync();

    private static ICollection<string> GetAddresses(ActivityExecutionContext context, Input<ICollection<string>> input)
    {
        return input.GetOrDefault(context) ?? new List<string>(0);
    }

    private async Task AddAttachmentsAsync(ActivityExecutionContext context, BodyBuilder bodyBuilder, CancellationToken cancellationToken)
    {
        var attachments = Attachments.GetOrDefault(context);

        if (attachments == null || attachments is string s && string.IsNullOrWhiteSpace(s))
            return;

        var index = 0;
        var attachmentObjects = InterpretAttachmentsModel(attachments);

        foreach (var attachmentObject in attachmentObjects)
        {
            switch (attachmentObject)
            {
                case Uri url:
                    await AttachOnlineFileAsync(context, bodyBuilder, url, cancellationToken);
                    break;
                case string path when path?.Contains("://") == true:
                    await AttachOnlineFileAsync(context, bodyBuilder, new Uri(path), cancellationToken);
                    break;
                case string path when !string.IsNullOrWhiteSpace(path):
                    await AttachLocalFileAsync(bodyBuilder, path, cancellationToken);
                    break;
                case byte[] bytes:
                    {
                        var fileName = $"Attachment-{++index}";
                        bodyBuilder.Attachments.Add(fileName, bytes, ContentType.Parse("application/binary"));
                        break;
                    }
                case Stream stream:
                    {
                        var fileName = $"Attachment-{++index}";
                        await bodyBuilder.Attachments.AddAsync(fileName, stream, ContentType.Parse("application/binary"), cancellationToken);
                        break;
                    }
                case EmailAttachment emailAttachment:
                    {
                        var fileName = emailAttachment.FileName ?? $"Attachment-{++index}";
                        var contentType = emailAttachment.ContentType ?? "application/binary";
                        var parsedContentType = ContentType.Parse(contentType);

                        if (emailAttachment.Content is byte[] bytes)
                            bodyBuilder.Attachments.Add(fileName, bytes, parsedContentType);

                        else if (emailAttachment.Content is Stream stream)
                            await bodyBuilder.Attachments.AddAsync(fileName, stream, parsedContentType, cancellationToken);

                        break;
                    }
                case ExpandoObject expandoObject:
                    {
                        var dictionary = new Dictionary<string, object>(expandoObject, StringComparer.OrdinalIgnoreCase);
                        var fileName = dictionary.GetValue<string>("FileName") ?? $"Attachment-{++index}";
                        var contentType = dictionary.GetValue<string>("ContentType") ?? "application/binary";
                        var parsedContentType = ContentType.Parse(contentType);
                        var content = dictionary.GetValue<object>("Content");

                        if (content is byte[] bytes)
                            bodyBuilder.Attachments.Add(fileName, bytes, parsedContentType);

                        else if (content is Stream stream)
                            await bodyBuilder.Attachments.AddAsync(fileName, stream, parsedContentType, cancellationToken);

                        break;
                    }
                default:
                    {
                        var json = JsonSerializer.Serialize(attachmentObject);
                        var fileName = $"Attachment-{++index}";
                        bodyBuilder.Attachments.Add(fileName, Encoding.UTF8.GetBytes(json), ContentType.Parse("application/json"));
                        break;
                    }
            }
        }
    }

    private async Task AttachLocalFileAsync(BodyBuilder bodyBuilder, string path, CancellationToken cancellationToken) => await bodyBuilder.Attachments.AddAsync(path, cancellationToken);

    private async Task AttachOnlineFileAsync(ActivityExecutionContext context, BodyBuilder bodyBuilder, Uri url, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(url.LocalPath);
        var downloader = context.GetRequiredService<IDownloader>();
        var response = await downloader.DownloadAsync(url, cancellationToken);
        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/binary";
        await bodyBuilder.Attachments.AddAsync(fileName, contentStream, ContentType.Parse(contentType), cancellationToken);
    }

    private IEnumerable InterpretAttachmentsModel(object attachments)
    {
        if (attachments is byte[] bytes)
            return new[]
            {
                bytes
            };

        return attachments is string text
            ? new[]
            {
                text
            }
            : attachments as IEnumerable ?? new[]
            {
                attachments
            };
    }

    private void SetRecipientsEmailAddresses(InternetAddressList list, IEnumerable<string>? addresses)
    {
        if (addresses == null)
            return;

        list.AddRange(addresses.Select(MailboxAddress.Parse));
    }
}