﻿namespace SlipeServer.Server.Elements;

public class RootElement : Element
{
    public override ElementType ElementType => ElementType.Root;

    public new RootElement AssociateWith(MtaServer server)
    {
        return server.AssociateElement(this);
    }
}
