﻿using Application.EventHandlers;

// MassTransit URN type resolutions, namespaces must be equal between projects for a shared type 
// ReSharper disable once CheckNamespace
namespace Contracts;

public class BusPositionsUpdated : Event
{
    public BusPositionsUpdated(Guid id, DateTime created) : base(id, created)
    {
    }
}