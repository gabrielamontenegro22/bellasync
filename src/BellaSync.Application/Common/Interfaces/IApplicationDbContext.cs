using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext usada desde la capa Application.
/// Application no depende de EF Core directamente más allá de DbSet,
/// y se mantiene desacoplada de Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Service> Services { get; }
    DbSet<Stylist> Stylists { get; }
    DbSet<StylistService> StylistServices { get; }
    DbSet<Customer> Customers { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<PaymentVoucher> PaymentVouchers { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<CommissionPayout> CommissionPayouts { get; }
    DbSet<CashClosing> CashClosings { get; }
    DbSet<SalonWeeklyHours> SalonWeeklyHours { get; }
    DbSet<SalonClosedDate> SalonClosedDates { get; }
    DbSet<WhatsAppTemplate> WhatsAppTemplates { get; }
    DbSet<WhatsAppMessage> WhatsAppMessages { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<SubscriptionInvoice> SubscriptionInvoices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
