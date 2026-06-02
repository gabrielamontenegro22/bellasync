using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnakeCaseNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renombrar las columnas internas de __EFMigrationsHistory a snake_case
            // ANTES de que EF intente leerlas para registrar esta migración.
            // EF crea esa tabla con columnas en PascalCase (MigrationId, ProductVersion)
            // pero después del UseSnakeCaseNamingConvention espera snake_case.
            // Idempotente: solo renombra si están en PascalCase.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = '__EFMigrationsHistory'
                                 AND column_name = 'MigrationId') THEN
                        ALTER TABLE ""__EFMigrationsHistory"" RENAME COLUMN ""MigrationId"" TO migration_id;
                        ALTER TABLE ""__EFMigrationsHistory"" RENAME COLUMN ""ProductVersion"" TO product_version;
                    END IF;
                END$$;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_tenants_TenantId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_password_reset_tokens_users_UserId",
                table: "password_reset_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_services_tenants_TenantId",
                table: "services");

            migrationBuilder.DropForeignKey(
                name: "FK_stylist_services_services_ServiceId",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "FK_stylist_services_stylists_StylistId",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "FK_stylist_services_tenants_TenantId",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "FK_stylists_tenants_TenantId",
                table: "stylists");

            migrationBuilder.DropForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_stylists",
                table: "stylists");

            migrationBuilder.DropIndex(
                name: "IX_stylists_TenantId_FullName",
                table: "stylists");

            migrationBuilder.DropIndex(
                name: "IX_stylists_UserId",
                table: "stylists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_stylist_services",
                table: "stylist_services");

            migrationBuilder.DropPrimaryKey(
                name: "PK_services",
                table: "services");

            migrationBuilder.DropIndex(
                name: "IX_services_TenantId_Name",
                table: "services");

            migrationBuilder.DropPrimaryKey(
                name: "PK_password_reset_tokens",
                table: "password_reset_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_customers",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_TenantId_Phone",
                table: "customers");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "users",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "users",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "users",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "users",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                table: "users",
                newName: "last_login_at");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "users",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "users",
                newName: "full_name");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "users",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_users_TenantId_Email",
                table: "users",
                newName: "ix_users_tenant_id_email");

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "tenants",
                newName: "slug");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "tenants",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "tenants",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "tenants",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "tenants",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "tenants",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                newName: "ix_tenants_slug");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "stylists",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "stylists",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "stylists",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "stylists",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Color",
                table: "stylists",
                newName: "color");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "stylists",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "stylists",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "stylists",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "stylists",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "IdNumber",
                table: "stylists",
                newName: "id_number");

            migrationBuilder.RenameColumn(
                name: "HireDate",
                table: "stylists",
                newName: "hire_date");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "stylists",
                newName: "full_name");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "stylists",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_stylists_TenantId",
                table: "stylists",
                newName: "ix_stylists_tenant_id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "stylist_services",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "AssignedAt",
                table: "stylist_services",
                newName: "assigned_at");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "stylist_services",
                newName: "service_id");

            migrationBuilder.RenameColumn(
                name: "StylistId",
                table: "stylist_services",
                newName: "stylist_id");

            migrationBuilder.RenameIndex(
                name: "IX_stylist_services_TenantId",
                table: "stylist_services",
                newName: "ix_stylist_services_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_stylist_services_ServiceId",
                table: "stylist_services",
                newName: "ix_stylist_services_service_id");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "services",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "services",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "services",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Color",
                table: "services",
                newName: "color");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "services",
                newName: "category");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "services",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "services",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "services",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "RequiresDeposit",
                table: "services",
                newName: "requires_deposit");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "services",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "DurationMinutes",
                table: "services",
                newName: "duration_minutes");

            migrationBuilder.RenameColumn(
                name: "DepositPercentage",
                table: "services",
                newName: "deposit_percentage");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "services",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CommissionPercentage",
                table: "services",
                newName: "commission_percentage");

            migrationBuilder.RenameIndex(
                name: "IX_services_TenantId",
                table: "services",
                newName: "ix_services_tenant_id");

            migrationBuilder.RenameColumn(
                name: "Token",
                table: "password_reset_tokens",
                newName: "token");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "password_reset_tokens",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "password_reset_tokens",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UsedAt",
                table: "password_reset_tokens",
                newName: "used_at");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "password_reset_tokens",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "password_reset_tokens",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "password_reset_tokens",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_password_reset_tokens_Token",
                table: "password_reset_tokens",
                newName: "ix_password_reset_tokens_token");

            migrationBuilder.RenameIndex(
                name: "IX_password_reset_tokens_UserId",
                table: "password_reset_tokens",
                newName: "ix_password_reset_tokens_user_id");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "customers",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "customers",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "customers",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Birthday",
                table: "customers",
                newName: "birthday");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "customers",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "customers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "customers",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "customers",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "customers",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "customers",
                newName: "full_name");

            migrationBuilder.RenameColumn(
                name: "DocumentNumber",
                table: "customers",
                newName: "document_number");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "customers",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "AcceptsMarketing",
                table: "customers",
                newName: "accepts_marketing");

            migrationBuilder.RenameIndex(
                name: "IX_customers_TenantId_FullName",
                table: "customers",
                newName: "ix_customers_tenant_id_full_name");

            migrationBuilder.RenameIndex(
                name: "IX_customers_TenantId",
                table: "customers",
                newName: "ix_customers_tenant_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_users",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_tenants",
                table: "tenants",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_stylists",
                table: "stylists",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_stylist_services",
                table: "stylist_services",
                columns: new[] { "stylist_id", "service_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_services",
                table: "services",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_password_reset_tokens",
                table: "password_reset_tokens",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_customers",
                table: "customers",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_stylists_tenant_id_full_name",
                table: "stylists",
                columns: new[] { "tenant_id", "full_name" },
                unique: true,
                filter: "status <> 2");

            migrationBuilder.CreateIndex(
                name: "ix_stylists_user_id",
                table: "stylists",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_services_tenant_id_name",
                table: "services",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_id_phone",
                table: "customers",
                columns: new[] { "tenant_id", "phone" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_tenants_tenant_id",
                table: "customers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_password_reset_tokens_users_user_id",
                table: "password_reset_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_services_tenants_tenant_id",
                table: "services",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stylist_services_services_service_id",
                table: "stylist_services",
                column: "service_id",
                principalTable: "services",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stylist_services_stylists_stylist_id",
                table: "stylist_services",
                column: "stylist_id",
                principalTable: "stylists",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_stylist_services_tenants_tenant_id",
                table: "stylist_services",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stylists_tenants_tenant_id",
                table: "stylists",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_tenants_tenant_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "fk_password_reset_tokens_users_user_id",
                table: "password_reset_tokens");

            migrationBuilder.DropForeignKey(
                name: "fk_services_tenants_tenant_id",
                table: "services");

            migrationBuilder.DropForeignKey(
                name: "fk_stylist_services_services_service_id",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "fk_stylist_services_stylists_stylist_id",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "fk_stylist_services_tenants_tenant_id",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "fk_stylists_tenants_tenant_id",
                table: "stylists");

            migrationBuilder.DropForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_tenants",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "pk_stylists",
                table: "stylists");

            migrationBuilder.DropIndex(
                name: "ix_stylists_tenant_id_full_name",
                table: "stylists");

            migrationBuilder.DropIndex(
                name: "ix_stylists_user_id",
                table: "stylists");

            migrationBuilder.DropPrimaryKey(
                name: "pk_stylist_services",
                table: "stylist_services");

            migrationBuilder.DropPrimaryKey(
                name: "pk_services",
                table: "services");

            migrationBuilder.DropIndex(
                name: "ix_services_tenant_id_name",
                table: "services");

            migrationBuilder.DropPrimaryKey(
                name: "pk_password_reset_tokens",
                table: "password_reset_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "pk_customers",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ix_customers_tenant_id_phone",
                table: "customers");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "users",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "users",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "users",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "users",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "last_login_at",
                table: "users",
                newName: "LastLoginAt");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "users",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "users",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "users",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_users_tenant_id_email",
                table: "users",
                newName: "IX_users_TenantId_Email");

            migrationBuilder.RenameColumn(
                name: "slug",
                table: "tenants",
                newName: "Slug");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "tenants",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "tenants",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "tenants",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "tenants",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "tenants",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                newName: "IX_tenants_Slug");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "stylists",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "stylists",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "stylists",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "stylists",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "color",
                table: "stylists",
                newName: "Color");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "stylists",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "stylists",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "stylists",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "stylists",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "id_number",
                table: "stylists",
                newName: "IdNumber");

            migrationBuilder.RenameColumn(
                name: "hire_date",
                table: "stylists",
                newName: "HireDate");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "stylists",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "stylists",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_stylists_tenant_id",
                table: "stylists",
                newName: "IX_stylists_TenantId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "stylist_services",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "assigned_at",
                table: "stylist_services",
                newName: "AssignedAt");

            migrationBuilder.RenameColumn(
                name: "service_id",
                table: "stylist_services",
                newName: "ServiceId");

            migrationBuilder.RenameColumn(
                name: "stylist_id",
                table: "stylist_services",
                newName: "StylistId");

            migrationBuilder.RenameIndex(
                name: "ix_stylist_services_tenant_id",
                table: "stylist_services",
                newName: "IX_stylist_services_TenantId");

            migrationBuilder.RenameIndex(
                name: "ix_stylist_services_service_id",
                table: "stylist_services",
                newName: "IX_stylist_services_ServiceId");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "services",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "services",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "services",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "color",
                table: "services",
                newName: "Color");

            migrationBuilder.RenameColumn(
                name: "category",
                table: "services",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "services",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "services",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "services",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "requires_deposit",
                table: "services",
                newName: "RequiresDeposit");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "services",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "duration_minutes",
                table: "services",
                newName: "DurationMinutes");

            migrationBuilder.RenameColumn(
                name: "deposit_percentage",
                table: "services",
                newName: "DepositPercentage");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "services",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "commission_percentage",
                table: "services",
                newName: "CommissionPercentage");

            migrationBuilder.RenameIndex(
                name: "ix_services_tenant_id",
                table: "services",
                newName: "IX_services_TenantId");

            migrationBuilder.RenameColumn(
                name: "token",
                table: "password_reset_tokens",
                newName: "Token");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "password_reset_tokens",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "password_reset_tokens",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "used_at",
                table: "password_reset_tokens",
                newName: "UsedAt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "password_reset_tokens",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "password_reset_tokens",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "password_reset_tokens",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_password_reset_tokens_token",
                table: "password_reset_tokens",
                newName: "IX_password_reset_tokens_Token");

            migrationBuilder.RenameIndex(
                name: "ix_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                newName: "IX_password_reset_tokens_UserId");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "customers",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "customers",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "customers",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "birthday",
                table: "customers",
                newName: "Birthday");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "customers",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "customers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "customers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "customers",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "customers",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "customers",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "document_number",
                table: "customers",
                newName: "DocumentNumber");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "customers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "accepts_marketing",
                table: "customers",
                newName: "AcceptsMarketing");

            migrationBuilder.RenameIndex(
                name: "ix_customers_tenant_id_full_name",
                table: "customers",
                newName: "IX_customers_TenantId_FullName");

            migrationBuilder.RenameIndex(
                name: "ix_customers_tenant_id",
                table: "customers",
                newName: "IX_customers_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                table: "tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_stylists",
                table: "stylists",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_stylist_services",
                table: "stylist_services",
                columns: new[] { "StylistId", "ServiceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_services",
                table: "services",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_password_reset_tokens",
                table: "password_reset_tokens",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_customers",
                table: "customers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_stylists_TenantId_FullName",
                table: "stylists",
                columns: new[] { "TenantId", "FullName" },
                unique: true,
                filter: "\"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_stylists_UserId",
                table: "stylists",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_services_TenantId_Name",
                table: "services",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Phone",
                table: "customers",
                columns: new[] { "TenantId", "Phone" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_customers_tenants_TenantId",
                table: "customers",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_password_reset_tokens_users_UserId",
                table: "password_reset_tokens",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_services_tenants_TenantId",
                table: "services",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stylist_services_services_ServiceId",
                table: "stylist_services",
                column: "ServiceId",
                principalTable: "services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stylist_services_stylists_StylistId",
                table: "stylist_services",
                column: "StylistId",
                principalTable: "stylists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stylist_services_tenants_TenantId",
                table: "stylist_services",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stylists_tenants_TenantId",
                table: "stylists",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Revertir el rename de las columnas internas de __EFMigrationsHistory.
            // Idempotente: solo renombra si están en snake_case.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = '__EFMigrationsHistory'
                                 AND column_name = 'migration_id') THEN
                        ALTER TABLE ""__EFMigrationsHistory"" RENAME COLUMN migration_id TO ""MigrationId"";
                        ALTER TABLE ""__EFMigrationsHistory"" RENAME COLUMN product_version TO ""ProductVersion"";
                    END IF;
                END$$;
            ");
        }
    }
}
