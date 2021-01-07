﻿using SlipeServer.Packets.Enums;
using SlipeServer.Packets.Definitions.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SlipeServer.Server.Elements;

namespace SlipeServer.Server.PacketHandling.QueueHandlers
{
    public class CommandQueueHandler : WorkerBasedQueueHandler
    {
        public CommandQueueHandler(int sleepInterval, int workerCount) : base(sleepInterval, workerCount) { }

        protected override void HandlePacket(PacketQueueEntry queueEntry)
        {
            switch (queueEntry.PacketId)
            {
                case PacketId.PACKET_ID_COMMAND:
                    CommandPacket commandPacket = new CommandPacket();
                    commandPacket.Read(queueEntry.Data);
                    queueEntry.Client.Player.TriggerCommand(commandPacket.Command, commandPacket.Arguments);
                    break;
            }
        }

    }
}
