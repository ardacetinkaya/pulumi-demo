using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Random;
using Storage = Pulumi.Azure.Storage;
using KeyVault = Pulumi.Azure.KeyVault;
using AppService = Pulumi.Azure.AppService;
using Redis = Pulumi.Azure.Redis;
using SQL = Pulumi.Azure.Sql;
using MSSQL = Pulumi.Azure.MSSql;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class Infrastructure : Stack
{
    public Infrastructure()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("InfrastructureSettings.json", true, false)
            .Build();

        var current = Output.Create(GetClientConfig.InvokeAsync());
        var sqlAdminPassword = new RandomString("random", new RandomStringArgs
        {
            Length = 16,
            OverrideSpecial = "=/:_@%",
            Special = true,
        });

        var resourceGroup = new ResourceGroup("product-resources", new ResourceGroupArgs
        {
            Name = "product-resources",
            Location = "West Europe"
        });

        var storageAccount = new Storage.Account("storage", new Storage.AccountArgs
        {
            Name = "productsa01",
            ResourceGroupName = resourceGroup.Name,
            Location = resourceGroup.Location,
            AccountReplicationType = "LRS",
            AccountTier = "Standard"
        });

        var logsContainer = new Storage.Container("logContainer", new Storage.ContainerArgs
        {
            Name = "logcontainer",
            StorageAccountName = storageAccount.Name,
            ContainerAccessType = "private",
        });

        var appServicePlan = new AppService.Plan("product-app-plan-01", new AppService.PlanArgs
        {

            Name = "product-app-plan-01",
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
            Sku = new AppService.Inputs.PlanSkuArgs
            {
                Tier = "Free",
                Size = "F1",
            }
        });
        var appKeyVault = Output.Create(KeyVault.GetKeyVault.InvokeAsync(new KeyVault.GetKeyVaultArgs
        {
            Name = "product-keyvault-01",
            ResourceGroupName = resourceGroup.GetResourceName()
        }));

        var cache = new Redis.Cache("product-cache-01", new Redis.CacheArgs
        {
            Name = "product-cache-01",
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
            Capacity = 0,
            Family = "C",
            SkuName = "Basic",
            EnableNonSslPort = false,
            MinimumTlsVersion = "1.2"
        });

        var apiAppService = new AppService.AppService("product-api-01", new AppService.AppServiceArgs
        {
            Name = "product-api-01",
            ResourceGroupName = resourceGroup.Name,
            Location = resourceGroup.Location,
            AppServicePlanId = appServicePlan.Id,
            HttpsOnly = true,
            Enabled = true,
            Identity = new AppService.Inputs.AppServiceIdentityArgs { Type = "SystemAssigned" },
            AppSettings = {
                { "KEYVAULT_ADDRESS",appKeyVault.Apply(k => k.VaultUri)},
                { "STORAGE_CONNECTIONSTRING",storageAccount.PrimaryConnectionString.Apply(con=>con)},
                { "CACHE_CONNECTIONSTRING",cache.PrimaryConnectionString.Apply(con=>con)}
            }

        });

        var webAppService = new AppService.AppService("product-www-01", new AppService.AppServiceArgs
        {
            Name = "product-www-01",
            ResourceGroupName = resourceGroup.Name,
            Location = resourceGroup.Location,
            AppServicePlanId = appServicePlan.Id,
            HttpsOnly = true,
            Enabled = true,
            Identity = new AppService.Inputs.AppServiceIdentityArgs { Type = "SystemAssigned" },
            AppSettings ={
                { "API_ADDRESS", $"https://{apiAppService.DefaultSiteHostname.Apply(adr=>adr)}"}
            }
        });



        var keyVault = new KeyVault.KeyVault("product-keyvault-01", new KeyVault.KeyVaultArgs
        {
            Name = "product-keyvault-01",
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
            EnabledForDiskEncryption = true,
            TenantId = current.Apply(current => current.TenantId),
            SoftDeleteEnabled = false,
            PurgeProtectionEnabled = false,
            SkuName = "standard",
            AccessPolicies =
            {
                new KeyVault.Inputs.KeyVaultAccessPolicyArgs
                {
                    TenantId = current.Apply(current => current.TenantId),
                    ObjectId = current.Apply(current => current.ObjectId),
                    KeyPermissions = { "get" },
                    SecretPermissions ={ "list","get","purge","recover","restore","set","backup","delete" },
                    CertificatePermissions ={ "get" }
                },
                new KeyVault.Inputs.KeyVaultAccessPolicyArgs
                {
                    TenantId = current.Apply(current => current.TenantId),
                    ObjectId = webAppService.Identity.Apply(id => id.PrincipalId ?? "11111111-1111-1111-1111-111111111111"),
                    KeyPermissions = { "get" },
                    SecretPermissions ={ "list","get" },
                    CertificatePermissions ={ "get" }
                },
                new KeyVault.Inputs.KeyVaultAccessPolicyArgs
                {
                    TenantId = current.Apply(current => current.TenantId),
                    ObjectId = apiAppService.Identity.Apply(id => id.PrincipalId ?? "11111111-1111-1111-1111-111111111111"),
                    KeyPermissions = { "get" },
                    SecretPermissions ={ "list","get" },
                    CertificatePermissions ={ "get" }
                },

            },
            NetworkAcls = new KeyVault.Inputs.KeyVaultNetworkAclsArgs
            {
                DefaultAction = "Allow",
                Bypass = "AzureServices",
            }
        });

        var keyVaultSecret01 = new KeyVault.Secret("sa-01-connection-string", new KeyVault.SecretArgs
        {
            Name = "sa-01-connection-string",
            KeyVaultId = keyVault.Id,
            ContentType = "text/plain",
            Value = storageAccount.PrimaryBlobConnectionString
        });

        var keyVaultSecret02 = new KeyVault.Secret("sql-admin-pwd", new KeyVault.SecretArgs
        {
            Name = "sql-admin-pwd",
            KeyVaultId = keyVault.Id,
            ContentType = "text/plain",
            Value = sqlAdminPassword.Result
        });


        var sqlServer = new SQL.SqlServer("product-sql-server-01", new SQL.SqlServerArgs
        {
            Name = "product-sql-server-01",
            ResourceGroupName = resourceGroup.Name,
            Location = resourceGroup.Location,
            Version = "12.0",
            AdministratorLogin = "4dm1n157r470r",
            AdministratorLoginPassword = sqlAdminPassword.Result
        });

        var database = new MSSQL.Database("product-db-01", new MSSQL.DatabaseArgs
        {
            Name = "product-db-01",
            ServerId = sqlServer.Id,
            Collation = "SQL_Latin1_General_CP1_CI_AS",
            MaxSizeGb = 2,
            SkuName = "Basic"
        });


        this.ConnectionString = storageAccount.PrimaryConnectionString;
        this.WebSiteAddress = webAppService.DefaultSiteHostname;

    }


    [Output]
    public Output<string> WebSiteAddress { get; set; }

    [Output]
    public Output<string> ConnectionString { get; set; }
}
