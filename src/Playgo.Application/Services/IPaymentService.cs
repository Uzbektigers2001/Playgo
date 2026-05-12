using Playgo.Application.Common;
using Playgo.Application.DTOs.Subscription;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public interface IPaymentService
{
    Task<Result<InitiatePaymentResult>> InitiateAsync(Guid userId, Guid planId, Guid? subscriptionId, PaymentProvider provider, CancellationToken cancellationToken = default);
    Task<Result> MarkCompletedAsync(Guid paymentId, string? providerTransactionId, string? rawResponse, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentDto>> GetMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentDto>> AdminGetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public interface IPaymentProviderService
{
    Task<string> CreatePaymentUrlAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<bool> VerifyCallbackAsync(string rawBody, IDictionary<string, string> headers, CancellationToken cancellationToken = default);
}
