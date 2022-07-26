﻿using Microsoft.Extensions.Logging;

internal class AppServicePlanQueryFilter : IQueryFilter
{
    private readonly WhatIfAfterBeforeChange afterState;
    private readonly ILogger logger;

    public AppServicePlanQueryFilter(WhatIfAfterBeforeChange afterState, ILogger logger)
    {
        this.afterState = afterState;
        this.logger = logger;
    }

    public string? GetFiltersBasedOnDesiredState()
    {
        var location = this.afterState.location;
        var sku = this.afterState.sku?.name;
        if (sku == null)
        {
            this.logger.LogError("Can't create a filter for App Service Plan when SKU is unavailable.");
            return null;
        }
           
        var serviceId = AppServicePlanSupportedData.SkuToServiceId[sku];
        string[] skuIds;

        if(IsLinuxPlan())
        {
            skuIds = AppServicePlanSupportedData.SkuToSkuIdMap[sku]
                .Where(_ => AppServicePlanSupportedData.LinuxSkuIds.Contains(_)).ToArray();
        }
        else
        {
            skuIds = AppServicePlanSupportedData.SkuToSkuIdMap[sku]
                .Where(_ => AppServicePlanSupportedData.LinuxSkuIds.Contains(_) == false).ToArray();
        }

        var skuIdsFilter = string.Join(" or ", skuIds.Select(_ => $"skuId eq '{_}'"));

        return $"$filter=serviceId eq '{serviceId}' and armRegionName eq '{location}' and ({skuIdsFilter})";
    }

    private bool IsLinuxPlan()
    {
        var isLinuxPlan = false;
        if (this.afterState.properties != null && this.afterState.properties.ContainsKey("reserved"))
        {
            var isReserved = this.afterState.properties["reserved"].ToString();
            if (isReserved != null)
            {
                isLinuxPlan = bool.Parse(isReserved);
            }
        }

        return isLinuxPlan;
    }
}
