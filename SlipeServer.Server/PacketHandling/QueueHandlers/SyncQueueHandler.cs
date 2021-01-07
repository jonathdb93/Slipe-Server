﻿using SlipeServer.Packets.Definitions.Sync;
using SlipeServer.Packets.Enums;
using SlipeServer.Server.Elements;
using SlipeServer.Server.Enums;
using SlipeServer.Server.Extensions;
using SlipeServer.Server.Repositories;


using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SlipeServer.Server.PacketHandling.QueueHandlers
{
    public class SyncQueueHandler : WorkerBasedQueueHandler
    {
        private readonly IElementRepository elementRepository;

        public SyncQueueHandler(IElementRepository elementRepository, int sleepInterval, int workerCount): base(sleepInterval, workerCount)
        {
            this.elementRepository = elementRepository;
        }

        protected override void HandlePacket(PacketQueueEntry queueEntry)
        {
            try
            { 
                switch (queueEntry.PacketId)
                {
                    case PacketId.PACKET_ID_CAMERA_SYNC:
                        CameraSyncPacket cameraPureSyncPacket = new CameraSyncPacket();
                        cameraPureSyncPacket.Read(queueEntry.Data);
                        HandleCameraSyncPacket(queueEntry.Client, cameraPureSyncPacket);
                        break;
                    case PacketId.PACKET_ID_PLAYER_KEYSYNC:
                        KeySyncPacket keySyncPacket = new KeySyncPacket();
                        keySyncPacket.Read(queueEntry.Data);
                        HandleClientKeySyncPacket(queueEntry.Client, keySyncPacket);
                        break;
                    case PacketId.PACKET_ID_PLAYER_PURESYNC:
                        PlayerPureSyncPacket playerPureSyncPacket = new PlayerPureSyncPacket();
                        playerPureSyncPacket.Read(queueEntry.Data);
                        HandleClientPureSyncPacket(queueEntry.Client, playerPureSyncPacket);
                        break;
                }
            } catch (Exception e)
            {
                Debug.WriteLine("Handling packet failed");
                Debug.WriteLine(string.Join(", ", queueEntry.Data));
                Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
            }
        }

        private void HandleCameraSyncPacket(Client client, CameraSyncPacket packet)
        {
            var player = client.Player;
            player.RunAsSync(() =>
            {
                if (packet.IsFixed)
                {
                    player.Camera.Position = packet.Position;
                    player.Camera.LookAt = packet.LookAt;
                } else
                {
                    player.Camera.Target = this.elementRepository.Get(packet.TargetId);
                }
            });
        }

        private void HandleClientKeySyncPacket(Client client, KeySyncPacket packet)
        {
            packet.PlayerId = client.Player.Id;
            packet.SendTo(this.elementRepository.GetByType<Player>(ElementType.Player).Where(p => p.Client != client));
        }

        private void HandleClientPureSyncPacket(Client client, PlayerPureSyncPacket packet)
        {
            client.SendPacket(new ReturnSyncPacket(packet.Position));

            packet.PlayerId = client.Player.Id;
            packet.Latency = 0;

            var otherPlayers = this.elementRepository
                .GetByType<Player>(ElementType.Player)
                .Where(p => p.Client != client);
            packet.SendTo(otherPlayers);

            var player = client.Player;
            player.RunAsSync(() =>
            {
                player.Position = packet.Position;
                player.Velocity = packet.Velocity;
                player.Health = packet.Health;
                player.Armor = packet.Armor;
                player.AimOrigin = packet.AimOrigin;
                player.AimDirection = packet.AimDirection;

                player.ContactElement = this.elementRepository.Get(packet.ContactElementId);

                player.CurrentWeapon = new PlayerWeapon()
                {
                    WeaponType = packet.WeaponType,
                    Slot = packet.WeaponSlot,
                    Ammo = packet.TotalAmmo,
                    AmmoInClip = packet.AmmoInClip
                };

                player.IsInWater = packet.SyncFlags.IsInWater;
                player.IsOnGround = packet.SyncFlags.IsOnGround;
                player.HasJetpack = packet.SyncFlags.HasJetpack;
                player.IsDucked = packet.SyncFlags.IsDucked;
                player.WearsGoggles = packet.SyncFlags.WearsGoggles;
                player.HasContact = packet.SyncFlags.HasContact;
                player.IsChoking = packet.SyncFlags.IsChoking;
                player.AkimboTargetUp = packet.SyncFlags.AkimboTargetUp;
                player.IsOnFire = packet.SyncFlags.IsOnFire;
                player.IsSyncingVelocity = packet.SyncFlags.IsSyncingVelocity;
                player.IsStealthAiming = packet.SyncFlags.IsStealthAiming;

                player.CameraPosition = packet.CameraOrientation.CameraPosition;
                player.CameraDirection = packet.CameraOrientation.CameraForward;
                player.CameraRotation = packet.CameraRotation;

                if (packet.IsDamageChanged)
                {
                    var damager = this.elementRepository.Get(packet.DamagerId);
                    player.TriggerDamaged(damager, (WeaponType)packet.DamageType, (BodyPart)packet.DamageBodypart);
                }
            });
        }
    }
}
