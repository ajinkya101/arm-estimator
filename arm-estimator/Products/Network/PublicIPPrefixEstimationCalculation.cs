﻿using Azure.Core;

internal class PublicIPPrefixEstimationCalculation : BaseEstimation, IEstimationCalculation
{
    public PublicIPPrefixEstimationCalculation(RetailItem[] items, ResourceIdentifier id, WhatIfAfterBeforeChange change)
        : base(items, id, change)
    {
    }

    public IOrderedEnumerable<RetailItem> GetItems()
    {
        return this.items.OrderByDescending(_ => _.retailPrice);
    }

    public double GetTotalCost(WhatIfChange[] changes)
    {
        double? estimatedCost = 0;
        var items = GetItems();

        foreach (var item in items)
        {
            estimatedCost += item.retailPrice * HoursInMonth;
        }

        return estimatedCost == null ? 0 : (double)estimatedCost;
    }
}
