﻿using System.Collections.Generic;
using System.Numerics;

namespace SlipeServer.Server.Elements;

public class Water : Element
{
    public override ElementType ElementType => ElementType.Water;

    public IEnumerable<Vector3> Vertices { get; set; }
    public bool IsShallow { get; set; } = false;

    public Water(IEnumerable<Vector3> vertices) : base()
    {
        this.Vertices = vertices;
    }

    public new Water AssociateWith(MtaServer server)
    {
        return server.AssociateElement(this);
    }
}
