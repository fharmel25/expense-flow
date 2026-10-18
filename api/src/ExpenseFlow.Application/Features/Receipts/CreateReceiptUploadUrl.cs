using ExpenseFlow.Application.Abstractions;
using MediatR;

namespace ExpenseFlow.Application.Features.Receipts;

/// <summary>
/// Step 1 of a submission: the app asks for a short-lived, write-only URL, then uploads the photo
/// directly to Blob Storage. The blob name is chosen by the server (receipts/{userId}/...), never by the client.
/// </summary>
public record CreateReceiptUploadUrlCommand : IRequest<ReceiptUploadTicket>;

public sealed class CreateReceiptUploadUrlHandler(IReceiptStorage receipts, ICurrentUser currentUser)
    : IRequestHandler<CreateReceiptUploadUrlCommand, ReceiptUploadTicket>
{
    public Task<ReceiptUploadTicket> Handle(CreateReceiptUploadUrlCommand cmd, CancellationToken ct) =>
        Task.FromResult(receipts.CreateUploadTicket(currentUser.UserId));
}
