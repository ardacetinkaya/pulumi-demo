using System.Collections.Generic;
using Pulumi;
using Pulumi.Azure.Core;
using Storage = Pulumi.Azure.Storage;
using CosmosDB = Pulumi.Azure.CosmosDB;
using KeyVault = Pulumi.Azure.KeyVault;
using AppService = Pulumi.Azure.AppService;
class Infrastructure : Stack
{
    public Infrastructure()
    {
        var current = Output.Create(GetClientConfig.InvokeAsync());

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
                { "STORAGE_CONNECTIONSTRING",storageAccount.PrimaryConnectionString.Apply(con=>con)}
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

        var keyVaultSecret = new KeyVault.Secret("sa-01-connection-string", new KeyVault.SecretArgs
        {
            Name = "sa-01-connection-string",
            KeyVaultId = keyVault.Id,
            ContentType = "text/plain",
            Value = storageAccount.PrimaryBlobConnectionString
        });



        this.ConnectionString = storageAccount.PrimaryConnectionString;
        this.WebSiteAddress = webAppService.DefaultSiteHostname;

    }

    [Output]
    public Output<string> WebSiteAddress { get; set; }

    [Output]
    public Output<string> ConnectionString { get; set; }
}
