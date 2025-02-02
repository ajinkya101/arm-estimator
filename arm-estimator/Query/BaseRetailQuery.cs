﻿using Azure.Core;
using Microsoft.Extensions.Logging;

internal abstract class BaseRetailQuery
{
    protected readonly WhatIfChange change;
    protected readonly ResourceIdentifier id;
    protected readonly ILogger logger;
    protected readonly string baseQuery;

    public BaseRetailQuery(WhatIfChange change, ResourceIdentifier id, ILogger logger, CurrencyCode currency)
    {
        this.change = change;
        this.id = id;
        this.logger = logger;
        this.baseQuery = $"https://prices.azure.com/api/retail/prices?currencyCode='{currency}'&$filter=priceType eq 'Consumption' and ";
    }

    public string? GetLocation()
    {
        var location = this.id.Location == null ? this.id?.Parent?.Location : this.id.Location;
        return location;
    }
}
