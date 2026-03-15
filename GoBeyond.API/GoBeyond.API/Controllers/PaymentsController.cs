using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(GoBeyondDbContext dbContext) : ControllerBase
{
    [Authorize(Policy = "ClientOnly")]
    [HttpPost("create-intent")]
    public async Task<PaymentIntentResultDto> CreateIntent(
        [FromBody] CreatePaymentIntentRequestDto request,
        CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var subscription = await dbContext.Subscriptions
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
            .FirstOrDefaultAsync(x => x.Id == request.SubscriptionId && x.ClientProfile.UserId == clientUserId, cancellationToken)
            ?? throw new InvalidOperationException("Subscription not found.");

        var latestSucceededPayment = subscription.Payments
            .OrderByDescending(x => x.Id)
            .FirstOrDefault(x => x.Status == PaymentStatus.Succeeded);

        if (subscription.Status == SubscriptionStatus.Active && latestSucceededPayment is not null)
        {
            return new PaymentIntentResultDto(
                DtoMapper.ToPaymentDto(latestSucceededPayment),
                DtoMapper.ToSubscriptionDto(subscription),
                "Subscription is already active.");
        }

        var payment = new Payment
        {
            SubscriptionId = subscription.Id,
            Amount = subscription.AmountPaid,
            Currency = "bam",
            StripePaymentIntentId = $"demo_{Guid.NewGuid():N}",
            Status = PaymentStatus.Succeeded
        };

        subscription.Status = SubscriptionStatus.Active;
        subscription.AmountPaid = subscription.MentorProfile.Price;

        dbContext.Payments.Add(payment);
        dbContext.Notifications.Add(new Notification
        {
            UserId = subscription.ClientProfile.UserId,
            Title = "Payment confirmed",
            Body = "Your subscription payment has been recorded and the mentor can now prepare the plan.",
            Type = NotificationType.PlanReady,
            IsRead = false
        });
        dbContext.Notifications.Add(new Notification
        {
            UserId = subscription.MentorProfile.UserId,
            Title = "Subscriber payment confirmed",
            Body = $"{subscription.ClientProfile.User.FirstName} {subscription.ClientProfile.User.LastName}".Trim() + " completed payment and is ready for onboarding.",
            Type = NotificationType.NewSubscriber,
            IsRead = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedSubscription = await LoadSubscriptionAsync(subscription.Id, cancellationToken);
        var createdPayment = updatedSubscription.Payments
            .OrderByDescending(x => x.Id)
            .First(x => x.StripePaymentIntentId == payment.StripePaymentIntentId);

        return new PaymentIntentResultDto(
            DtoMapper.ToPaymentDto(createdPayment),
            DtoMapper.ToSubscriptionDto(updatedSubscription),
            "Demo payment completed successfully.");
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<PaymentDto> StripeWebhook(
        [FromBody] ProcessPaymentWebhookRequestDto request,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.StripePaymentIntentId == request.PaymentIntentId, cancellationToken)
            ?? throw new InvalidOperationException("Payment intent not found.");

        payment.Status = DtoMapper.ParsePaymentStatus(request.Status);

        payment.Subscription.Status = payment.Status == PaymentStatus.Succeeded
            ? SubscriptionStatus.Active
            : SubscriptionStatus.Pending;

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToPaymentDto(payment);
    }

    private async Task<Subscription> LoadSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken)
        => await dbContext.Subscriptions
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
            .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException("Subscription not found.");
}
