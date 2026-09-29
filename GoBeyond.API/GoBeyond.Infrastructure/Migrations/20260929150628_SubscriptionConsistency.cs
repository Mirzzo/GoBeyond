using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoBeyond.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Duplikati nastali prije ovog indeksa (istovremeni POST /api/subscriptions): višak neplaćenih PendingPayment
            // pretplata klijenta koji ima i noviju ili odmakliju otvorenu pretplatu se otkazuje. Nema naplate pa ni povrata
            // (PaymentIntent koji bi ipak bio plaćen usklađivanje otkazuje ili vraća). Plaćeni duplikati (AwaitingMentor/Active)
            // se ne mijenjaju automatski: tada kreiranje indeksa ne uspijeva i duplikat se rješava ručno (uz povrat).
            migrationBuilder.Sql("""
                UPDATE s
                SET s.Status = 5, s.CancelledAt = SYSUTCDATETIME(), s.StatusReason = N'Dvostruka pretplata je automatski otkazana.'
                FROM Subscriptions AS s
                WHERE s.Status = 1 AND EXISTS (
                    SELECT 1 FROM Subscriptions AS o
                    WHERE o.ClientProfileId = s.ClientProfileId AND o.Id <> s.Id AND o.Status IN (1, 2, 3)
                      AND (o.Status > 1 OR o.Id > s.Id));
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ClientProfileId_Open",
                table: "Subscriptions",
                column: "ClientProfileId",
                unique: true,
                filter: "[Status] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_ClientProfileId_Open",
                table: "Subscriptions");
        }
    }
}
