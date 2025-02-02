﻿using Azure.Core;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

internal class WhatIfProcessor
{
    private static readonly Lazy<HttpClient> httpClient = new(() => new HttpClient());
    private static readonly Dictionary<string, string> parentResourceToLocation = new();
    private static readonly Dictionary<string, RetailAPIResponse> cachedResults = new();

    private readonly ILogger logger;
    private readonly WhatIfChange[] changes;
    private readonly CurrencyCode currency;
    private readonly bool disableDetailedMetrics;

    public WhatIfProcessor(ILogger logger, WhatIfChange[] changes, CurrencyCode currency, bool disableDetailedMetrics)
    {
        this.logger = logger;
        this.changes = changes;
        this.currency = currency;
        this.disableDetailedMetrics = disableDetailedMetrics;
    }

    public async Task<EstimationOutput> Process()
    {
        double totalCost = 0;
        double delta = 0;

        this.logger.LogInformation("Estimations:");
        this.logger.LogInformation("");

        var resources = new List<EstimatedResourceData>();
        var unsupportedResources = new List<ResourceIdentifier>();

        foreach (WhatIfChange change in this.changes)
        {
            if (change.resourceId == null)
            {
                logger.LogWarning("Ignoring resource with empty resource ID");
                continue;
            }

            if (change.after == null && change.before == null)
            {
                logger.LogWarning("Ignoring resource with empty desired state.");
                continue;
            }

            var id = new ResourceIdentifier(change.resourceId);
            EstimatedResourceData? resource = null;
            switch (id?.ResourceType)
            {
                case "Microsoft.Storage/storageAccounts":
                    resource = await Calculate<StorageAccountRetailQuery, StorageAccountEstimationCalculation>(change, id);
                    break;
                case "Microsoft.ContainerRegistry/registries":
                    resource = await Calculate<ContainerRegistryRetailQuery, ContainerRegistryEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Web/serverfarms":
                    resource = await Calculate<AppServicePlanRetailQuery, AppServicePlanEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Web/sites":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.ContainerService/managedClusters":
                    resource = await Calculate<AKSRetailQuery, AKSEstimationCalculation>(change, id);
                    break;
                case "Microsoft.App/containerApps":
                    resource = await Calculate<ContainerAppsRetailQuery, ContainerAppsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Sql/servers":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Sql/servers/databases":
                    resource = await Calculate<SQLRetailQuery, SQLEstimationCalculation>(change, id);
                    break;
                case "Microsoft.ApiManagement/service":
                    resource = await Calculate<APIMRetailQuery, APIMEstimationCalculation>(change, id);
                    break;
                case "Microsoft.ApiManagement/service/gateways":
                    resource = await Calculate<APIMRetailQuery, APIMEstimationCalculation>(change, id);
                    break;
                case "Microsoft.AppConfiguration/configurationStores":
                    resource = await Calculate<AppConfigurationRetailQuery, AppConfigurationEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/applicationGateways":
                    resource = await Calculate<ApplicationGatewayRetailQuery, ApplicationGatewayEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Insights/components":
                    resource = await Calculate<ApplicationInsightsRetailQuery, ApplicationInsightsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.AnalysisServices/servers":
                    resource = await Calculate<AnalysisServicesRetailQuery, AnalysisServicesEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/bastionHosts":
                    resource = await Calculate<BastionRetailQuery, BastionEstimationCalculation>(change, id);
                    break;
                case "Microsoft.BotService/botServices":
                    resource = await Calculate<BotServiceRetailQuery, BotServiceEstimationCalculation>(change, id);
                    break;
                case "Microsoft.HealthBot/healthBots":
                    resource = await Calculate<HealthBotServiceRetailQuery, HealthBotServiceEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Chaos/experiments":
                    resource = await Calculate<ChaosRetailQuery, ChaosEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Search/searchServices":
                    resource = await Calculate<CognitiveSearchRetailQuery, CognitiveSearchEstimationCalculation>(change, id);
                    break;
                case "Microsoft.ConfidentialLedger/ledgers":
                    resource = await Calculate<ConfidentialLedgerRetailQuery, ConfidentialLedgerEstimationCalculation>(change, id);
                    break;
                case "Microsoft.DocumentDB/databaseAccounts":
                    resource = await Calculate<CosmosDBRetailQuery, CosmosDBEstimationCalculation>(change, id);
                    break;
                case "Microsoft.DocumentDB/databaseAccounts/sqlDatabases":
                    resource = await Calculate<CosmosDBRetailQuery, CosmosDBEstimationCalculation>(change, id);
                    break;
                case "Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers":
                    resource = await Calculate<CosmosDBRetailQuery, CosmosDBEstimationCalculation>(change, id);
                    break;
                case "Microsoft.EventHub/namespaces":
                    resource = await Calculate<EventHubRetailQuery, EventHubEstimationCalculation>(change, id);
                    break;
                case "Microsoft.EventHub/namespaces/eventhubs":
                    resource = await Calculate<EventHubRetailQuery, EventHubEstimationCalculation>(change, id);
                    break;
                case "Microsoft.EventHub/clusters":
                    resource = await Calculate<EventHubRetailQuery, EventHubEstimationCalculation>(change, id);
                    break;
                case "Microsoft.StreamAnalytics/clusters":
                    resource = await Calculate<StreamAnalyticsRetailQuery, StreamAnalyticsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.StreamAnalytics/streamingjobs":
                    resource = await Calculate<StreamAnalyticsRetailQuery, StreamAnalyticsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.KeyVault/vaults":
                    resource = await Calculate<KeyVaultRetailQuery, KeyVaultEstimationCalculation>(change, id);
                    break;
                case "Microsoft.KeyVault/managedHSMs":
                    resource = await Calculate<KeyVaultRetailQuery, KeyVaultEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/virtualNetworkGateways":
                    resource = await Calculate<VPNGatewayRetailQuery, VPNGatewayEstimationCalculation>(change, id);
                    break;
                case "Microsoft.SignalRService/signalR":
                    resource = await Calculate<SignalRRetailQuery, SignalREstimationCalculation>(change, id);
                    break;
                case "Microsoft.TimeSeriesInsights/environments":
                    resource = await Calculate<TimeSeriesRetailQuery, TimeSeriesEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Logic/workflows":
                    resource = await Calculate<LogicAppsRetailQuery, LogicAppsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Logic/integrationAccounts":
                    resource = await Calculate<LogicAppsRetailQuery, LogicAppsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.EventGrid/systemTopics":
                    resource = await Calculate<EventGridRetailQuery, EventGridEstimationCalculation>(change, id);
                    break;
                case "Microsoft.EventGrid/topics":
                    resource = await Calculate<EventGridRetailQuery, EventGridEstimationCalculation>(change, id);
                    break;
                case "Microsoft.EventGrid/eventSubscriptions":
                    resource = await Calculate<EventGridRetailQuery, EventGridEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Compute/virtualMachines":
                    resource = await Calculate<VirtualMachineRetailQuery, VirtualMachineEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/publicIPPrefixes":
                    resource = await Calculate<PublicIPPrefixRetailQuery, PublicIPPrefixEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/publicIPAddresses":
                    resource = await Calculate<PublicIPRetailQuery, PublicIPEstimationCalculation>(change, id);
                    break;
                case "Microsoft.OperationalInsights/workspaces":
                    resource = await Calculate<LogAnalyticsRetailQuery, LogAnalyticsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.OperationsManagement/solutions":
                    resource = await Calculate<LogAnalyticsRetailQuery, LogAnalyticsEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/networkInterfaces":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Network/networkSecurityGroups":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Network/virtualNetworks":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.RecoveryServices/vaults/backupPolicies":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.RecoveryServices/vaults":
                    resource = await Calculate<RecoveryServicesRetailQuery, RecoveryServicesEstimationCalculation>(change, id);
                    break;
                case "Microsoft.RecoveryServices/vaults/backupFabrics/protectionContainers/protectedItems":
                    resource = await Calculate<RecoveryServicesProtectedItemRetailQuery, RecoveryServicesProtectedItemEstimationCalculation>(change, id);
                    break;
                case "Microsoft.RecoveryServices/vaults/replicationFabrics":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.RecoveryServices/vaults/replicationFabrics/replicationNetworks/replicationNetworkMappings":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.RecoveryServices/vaults/replicationFabrics/replicationProtectionContainers":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.RecoveryServices/vaults/replicationFabrics/replicationProtectionContainers/replicationProtectionContainerMappings":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.RecoveryServices/vaults/replicationFabrics/replicationProtectionContainers/replicationProtectedItems":
                    resource = await Calculate<AzureSiteRecoveryRetailQuery, AzureSiteRecoveryEstimationCalculation>(change, id);
                    break;
                case "Microsoft.RecoveryServices/vaults/replicationPolicies":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Insights/metricAlerts":
                    resource = await Calculate<MonitorRetailQuery, MonitorEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Insights/scheduledQueryRules":
                    resource = await Calculate<MonitorRetailQuery, MonitorEstimationCalculation>(change, id);
                    break;
                case "Microsoft.DBforMariaDB/servers":
                    resource = await Calculate<MariaDBRetailQuery, MariaDBEstimationCalculation>(change, id);
                    break;
                case "Microsoft.DBforMariaDB/servers/virtualNetworkRules":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Cache/redis":
                    resource = await Calculate<RedisRetailQuery, RedisEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Network/ipGroups":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Network/firewallPolicies":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Network/firewallPolicies/ruleCollectionGroups":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Network/azureFirewalls":
                    resource = await Calculate<FirewallRetailQuery, FirewallEstimationCalculation>(change, id);
                    break;
                case "Microsoft.Storage/storageAccounts/blobServices":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                case "Microsoft.Storage/storageAccounts/blobServices/containers":
                    resource = ReportResourceWithoutCost(id, change.changeType);
                    break;
                default:
                    if(id?.Name != null)
                    {
                        unsupportedResources.Add(id);
                    }
                    
                    break;
            }

            if (resource == null) continue;
            if (change.changeType != WhatIfChangeType.Delete)
            {
                totalCost += resource.TotalCost;
            }

            if (change.changeType == WhatIfChangeType.Create)
            {
                delta += resource.TotalCost;
            }
            else if (change.changeType == WhatIfChangeType.Delete)
            {
                delta -= resource.TotalCost;
            }

            resources.Add(resource);
        }

        var sign = "+";
        if (delta < 0)
        {
            sign = "";
        }

        if(resources.Count == 0)
        {
            this.logger.AddEstimatorMessage("No resource available for estimation.");
            this.logger.LogInformation("");
            this.logger.LogInformation("-------------------------------");
            this.logger.LogInformation("");
        }

        if(unsupportedResources.Count > 0)
        {
            this.logger.LogInformation("Unsupported resources:");
            this.logger.LogInformation("");
            ReportUnsupportedResources(unsupportedResources);
            this.logger.LogInformation("");
            this.logger.LogInformation("-------------------------------");
            this.logger.LogInformation("");
        }

        this.logger.LogInformation("Summary:");
        this.logger.LogInformation("");
        this.logger.AddEstimatorMessage("Total cost: {0} {1}", totalCost.ToString("N2"), this.currency);
        this.logger.AddEstimatorMessage("Delta: {0}{1} {2}", sign, delta.ToString("N2"), this.currency);
        this.logger.LogInformation("");

        return new EstimationOutput(totalCost, delta, resources, currency);
    }

    private void ReportUnsupportedResources(List<ResourceIdentifier> unsupportedResources)
    {
        foreach(var resource in unsupportedResources)
        {
            this.logger.AddEstimatorMessage("{0} [{1}]", resource.Name, resource.ResourceType);
        }
    }

    private async Task<EstimatedResourceData?> Calculate<TQuery, TCalculation>(WhatIfChange change, ResourceIdentifier id)
        where TQuery : BaseRetailQuery, IRetailQuery
        where TCalculation : BaseEstimation, IEstimationCalculation
    {
        var data = await GetRetailAPIResponse<TQuery>(change, id);
        if (data == null || data.Items == null)
        {
            this.logger.LogWarning("Got no records for {type} from Retail API", id.ResourceType);
            this.logger.LogInformation("");

            return null;
        }

        if (change.resourceId == null || (change.after == null && change.before == null))
        {
            this.logger.LogError("No data available for WhatIf operation.");
            return null;
        }

        var desiredState = change.after ?? change.before;
        if(desiredState == null)
        {
            this.logger.LogError("No data available for WhatIf operation.");
            return null;
        }

        if (Activator.CreateInstance(typeof(TCalculation), new object[] { data.Items, id, desiredState }) is not TCalculation estimation)
        {
            this.logger.LogError("Couldn't create an instance of {type}.", typeof(TCalculation));
            return null;
        }

        var totalCost = estimation.GetTotalCost(this.changes);

        double? delta = null;
        if(change.before != null)
        {
            if (Activator.CreateInstance(typeof(TCalculation), new object[] { data.Items, id, desiredState }) is not TCalculation previousStateEstimation)
            {
                this.logger.LogError("Couldn't create an instance of {type}.", typeof(TCalculation));
            }
            else
            {
                var previousCost = previousStateEstimation.GetTotalCost(this.changes);
                delta = totalCost - previousCost;
            }
        }

        ReportEstimationToConsole(id, estimation.GetItems(), totalCost, change.changeType, delta, data.Items?.FirstOrDefault()?.location);
        return new EstimatedResourceData(totalCost, delta, id);
    }

    private async Task<RetailAPIResponse?> GetRetailAPIResponse<T>(WhatIfChange change, ResourceIdentifier id) where T : BaseRetailQuery, IRetailQuery
    {
        var desiredState = change.after ?? change.before;
        if (desiredState == null || change.resourceId == null)
        {
            this.logger.LogError("Couldn't determine desired state for {type}.", typeof(T));
            return null;
        }

        if (desiredState.location != null)
        {
            parentResourceToLocation.Add(change.resourceId, desiredState.location);
        }

        if (Activator.CreateInstance(typeof(T), new object[] { change, id, logger, this.currency }) is not T query)
        {
            this.logger.LogError("Couldn't create an instance of {type}.", typeof(T));
            return null;
        }

        var location = desiredState.location ?? parentResourceToLocation[FindParentId(id)];
        if (location == null)
        {
            this.logger.LogError("Resources without location are not supported.");
            return null;
        }

        string? url;
        try
        {
            url = query.GetQueryUrl(location);
            if (url == null)
            {
                this.logger.LogError("URL generated for {type} is null.", typeof(T));
                return null;
            }
        }
        catch(KeyNotFoundException)
        {
            this.logger.LogWarning("{name} ({type}) [SKU is not yet supported - {sku}]", id.Name, id.ResourceType, desiredState.sku?.name);
            return null;
        }


        var data = await TryGetCachedResultForUrl(url);
        if (data == null || data.Items == null)
        {
            this.logger.LogWarning("Data for {resourceType} is not available.", id.ResourceType);
            return null;
        }

        return data;
    }

    private async Task<RetailAPIResponse?> TryGetCachedResultForUrl(string url)
    {
        RetailAPIResponse? data;
        var urlHash = Convert.ToBase64String(Encoding.UTF8.GetBytes(url));
        if (cachedResults.TryGetValue(urlHash, out var previousResponse))
        {
            this.logger.LogDebug("Getting Retail API data for {url} from cache.", url);
            data = previousResponse;
        }
        else
        {
            var response = await GetRetailDataResponse(url);
            if(response.IsSuccessStatusCode == false)
            {
                return null;
            }

            data = JsonSerializer.Deserialize<RetailAPIResponse>(await response.Content.ReadAsStreamAsync());

            if (data != null)
            {
                cachedResults.Add(urlHash, data);
            }
        }

        return data;
    }

    private string FindParentId(ResourceIdentifier id)
    {
        var currentParent = id.Parent;
        var parentType = currentParent?.Parent?.ResourceType;

        while(parentType != "Microsoft.Resources/resourceGroups" && parentType != "Microsoft.Resources/subscriptions")
        {
            currentParent = currentParent?.Parent;
            parentType = currentParent?.Parent?.ResourceType;
        }

        if(currentParent?.Name == null)
        {
            throw new Exception("Couldn't find resource parent.");
        }

        return currentParent.ToString();
    }

    private async Task<HttpResponseMessage> GetRetailDataResponse(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await httpClient.Value.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return response;
        }
        else
        {
            response.EnsureSuccessStatusCode();
            return response;
        }
    }

    private void ReportEstimationToConsole(ResourceIdentifier id, IOrderedEnumerable<RetailItem> items, double? totalCost, WhatIfChangeType? changeType, double? delta, string? location)
    {
        var deltaSign = delta == null ? "+" : delta == 0 ? "" : "-";
        delta = delta == null ? totalCost : 0;

        this.logger.AddEstimatorMessageSensibleToChange(changeType, "{0}", id.Name);
        this.logger.AddEstimatorMessageSubsection("Type: {0}", id.ResourceType);
        this.logger.AddEstimatorMessageSubsection("Location: {0}", location);
        this.logger.AddEstimatorMessageSubsection("Total cost: {0} {1}", totalCost?.ToString("N2"), this.currency);
        this.logger.AddEstimatorMessageSubsection("Delta: {0} {1}", $"{deltaSign}{delta.GetValueOrDefault().ToString("N2")}", this.currency);

        if(this.disableDetailedMetrics == false)
        {
            ReportDetailedMetrics(items);
        }

        this.logger.LogInformation("");
        this.logger.LogInformation("-------------------------------");
        this.logger.LogInformation("");
    }

    private void ReportDetailedMetrics(IOrderedEnumerable<RetailItem> items)
    {
        this.logger.LogInformation("");
        this.logger.LogInformation("Aggregated metrics:");
        this.logger.LogInformation("");

        if (items.Any())
        {
            foreach (var item in items)
            {
                this.logger.LogInformation("-> {skuName} | {productName} | {meterName} | {retailPrice} for {measure}", item.skuName, item.productName, item.meterName, item.retailPrice, item.unitOfMeasure);
            }
        }
        else
        {
            this.logger.LogInformation("No metrics available.");
        }
    }

    private EstimatedResourceData ReportResourceWithoutCost(ResourceIdentifier id, WhatIfChangeType? changeType)
    {
        this.logger.AddEstimatorMessageSensibleToChange(changeType, "{0}", id.Name);
        this.logger.AddEstimatorMessageSubsection("Type: {0}", id.ResourceType);
        this.logger.AddEstimatorMessageSubsection("Total cost: Free");
        this.logger.LogInformation("");
        this.logger.LogInformation("-------------------------------");
        this.logger.LogInformation("");

        return new EstimatedResourceData(0, 0, id);
    }
}
