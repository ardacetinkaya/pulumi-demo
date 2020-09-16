# pulumi-demo

This is a simple Pulumi demonstration for creating Azure resources. 


- Storage Account
  - Container
- App Service
- KeyVault
  - Secret

## Prerequisites
- [Install Pulumi](https://www.pulumi.com/docs/get-started/install/)
- [Install .NET Core 3.0+](https://dotnet.microsoft.com/download)

## Steps
1. Create a new directory.
```
mkdir pulumi-demo
```
2. Create a new Pulumi project.
```
cd pulumi-demo
pulumi new
```
3. Choose _azure-csharp_ template and complete the project options(you may use default values)
4. Set environment varibles for your Azure Subscription.
```
pulumi config set azure:clientId <XXXXXXXXXXXXX>
pulumi config set azure:clientSecret <XXXXXXXXXXXXX> --secret
pulumi config set azure:tenantId <XXXXXXXXXXXXX>
pulumi config set azure:subscriptionId <XXXXXXXXXXXXX>

```
5. Change MyStack.cs file content with Infrastructure.cs file and rename MyStack class in Program.cs
6. Run pulumi up to see new infrastructure resources.
```
pulumi up
```
7. Check the output and follow the screen instructions.
```
.......
....
...
Resources:
    + 2 to create
    ~ 1 to update
    - 1 to delete
    4 changes. 6 unchanged
    
Do you want to perform this update?  [Use arrows to move, enter to select, type to filter]
  yes
> no
  details
````  
 8. If you have choosen _yes_ after some time the deployment of your resources will finish, time to go and check Azure Portal.



