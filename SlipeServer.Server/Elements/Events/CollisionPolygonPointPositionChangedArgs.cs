﻿using SlipeServer.Server.Elements.ColShapes;
using System;
using System.Numerics;

namespace SlipeServer.Server.Elements.Events;

public class CollisionPolygonPointPositionChangedArgs : EventArgs
{
    public CollisionPolygon Polygon { get; set; }
    public uint Index { get; set; }
    public Vector2 Position { get; set; }

    public CollisionPolygonPointPositionChangedArgs(CollisionPolygon polygon, uint index, Vector2 position)
    {
        this.Polygon = polygon;
        this.Index = index;
        this.Position = position;
    }
}
