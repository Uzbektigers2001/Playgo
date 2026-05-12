using Playgo.Application.Services;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Services.Payments;

public class ManualPaymentProvider : IPaymentProviderService
{
    public Task<string> CreatePaymentUrlAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var url = $"https://demo-payment/manual?paymentId={payment.Id}";
        return Task.FromResult(url);
    }

    public Task<bool> VerifyCallbackAsync(string rawBody, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        // TODO: real signature validation; manual is admin-controlled
        return Task.FromResult(true);
    }
}
