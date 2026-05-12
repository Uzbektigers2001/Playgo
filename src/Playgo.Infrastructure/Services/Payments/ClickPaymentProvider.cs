using Playgo.Application.Services;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Services.Payments;

public class ClickPaymentProvider : IPaymentProviderService
{
    public Task<string> CreatePaymentUrlAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var url = $"https://demo-payment/click?paymentId={payment.Id}&amount={payment.Amount}";
        return Task.FromResult(url);
    }

    public Task<bool> VerifyCallbackAsync(string rawBody, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        // TODO: implement Click signature validation
        return Task.FromResult(true);
    }
}
