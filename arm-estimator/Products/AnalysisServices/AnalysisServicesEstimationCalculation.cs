﻿using Azure.Core;

internal class AnalysisServicesEstimationCalculation : BaseEstimation, IEstimationCalculation
{
    public AnalysisServicesEstimationCalculation(RetailItem[] items, ResourceIdentifier id, WhatIfAfterBeforeChange change)
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
        int? capacity = 1;

        if(change.sku != null)
        {
            if(change.sku.capacity != null)
            {
                capacity = change.sku.capacity;
            }
        }

        foreach (var item in items)
        {
            if(item.meterName == "S0 Scale-Out")
            {
                if(capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S1 scale-out
            else if (item.meterName == "S1 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S2 scale-out
            else if (item.meterName == "S2 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S4 scale-out
            else if (item.meterName == "S4 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S8 scale-out
            else if (item.meterName == "S8 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S9 scale-out
            else if (item.meterName == "S9 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S8 V2 scale-out
            else if (item.meterName == "S8 v2 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            // S9 V2 scale-out
            else if (item.meterName == "S9 v2 Scale-Out")
            {
                if (capacity >= 1)
                {
                    estimatedCost += item.retailPrice * HoursInMonth * capacity;
                }
            }
            else
            {
                estimatedCost += item.retailPrice * HoursInMonth;
            }
        }

        return estimatedCost == null ? 0 : (double)estimatedCost;
    }
}
