﻿using SlipeServer.Server.Elements;
using SlipeServer.Server.Enums;
using SlipeServer.Server.Extensions;
using SlipeServer.Server.ElementCollections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SlipeServer.Server.AllSeeingEye;

public class AseQueryService : IAseQueryService
{
    private readonly MtaServer mtaServer;
    private readonly Configuration configuration;
    private readonly IElementCollection elementCollection;
    private readonly AseVersion aseVersion;
    private readonly Dictionary<string, string> rules;

    public AseQueryService(MtaServer mtaServer, Configuration configuration, IElementCollection elementCollection)
    {
        this.mtaServer = mtaServer;
        this.configuration = configuration;
        this.elementCollection = elementCollection;

        this.aseVersion = AseVersion.v1_5;

        this.rules = new Dictionary<string, string>();
    }

    public void SetRule(string key, string value)
    {
        this.rules[key] = value;
    }

    public bool RemoveRule(string key) => this.rules.Remove(key);

    public string? GetRule(string key)
    {
        this.rules.TryGetValue(key, out string? value);
        return value;
    }


    public byte[] QueryFull(ushort port)
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        IEnumerable<Player> players = this.elementCollection.GetByType<Player>(ElementType.Player);

        string aseVersion = GetVersion(this.aseVersion);

        bw.Write("EYE1".AsSpan());
        bw.WriteWithLength("mta");
        bw.WriteWithLength(port);
        bw.WriteWithLength(this.configuration.ServerName);
        bw.WriteWithLength(this.mtaServer.GameType);
        bw.WriteWithLength(this.mtaServer.MapName);
        bw.WriteWithLength(aseVersion);
        bw.WriteWithLength(this.mtaServer.HasPassword);
        bw.WriteWithLength(players.Count().ToString());
        bw.WriteWithLength(this.configuration.MaxPlayerCount.ToString());
        foreach (var item in this.rules)
        {
            bw.WriteWithLength(item.Key);
            bw.WriteWithLength(item.Value);
        }
        bw.Write((byte)1);

        byte flags = 0;
        flags |= (byte)PlayerFlags.Nick;
        flags |= (byte)PlayerFlags.Team;
        flags |= (byte)PlayerFlags.Skin;
        flags |= (byte)PlayerFlags.Score;
        flags |= (byte)PlayerFlags.Ping;
        flags |= (byte)PlayerFlags.Time;

        foreach (Player player in players)
        {
            bw.Write(flags);
            bw.WriteWithLength(player.Name.StripColorCode());
            bw.Write((byte)1); // team, skip
            bw.Write((byte)1); // skin, skip
            bw.WriteWithLength(1); // score
            bw.WriteWithLength((int)player.Client.Ping);
            bw.Write((byte)1); // time, skip
        }

        bw.Flush();
        return stream.ToArray();
    }

    public byte[] QueryXFireLight()
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        int playerCount = this.elementCollection.GetByType<Player>(ElementType.Player).Count();
        string strPlayerCount = playerCount + "/" + this.configuration.MaxPlayerCount;

        bw.Write("EYE3".AsSpan());
        bw.WriteWithLength("mta");
        bw.WriteWithLength(this.configuration.ServerName);
        bw.WriteWithLength(this.mtaServer.GameType);

        bw.Write((byte)(this.mtaServer.MapName.Length + strPlayerCount.Length + 2));
        bw.Write(this.mtaServer.MapName.AsSpan());
        bw.Write((byte)0);
        bw.Write(strPlayerCount.AsSpan());  // client double checks this field in clientside against fake players count function:
                                            // "CCore::GetSingleton().GetNetwork()->UpdatePingStatus(*strPingStatus, info.players);" 
        bw.WriteWithLength(GetVersion(this.aseVersion));
        bw.Write((byte)(this.mtaServer.HasPassword ? 1 : 0));
        bw.Write((byte)Math.Min(playerCount, 255));
        bw.Write((byte)Math.Min(this.configuration.MaxPlayerCount, (ushort)255));

        bw.Flush();
        return stream.ToArray();
    }


    public byte[] QueryLight(ushort port, VersionType version = VersionType.Release)
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        List<string> playerNames = this.elementCollection.GetByType<Player>(ElementType.Player)
            .Select(o => o.Name.StripColorCode())
            .ToList();

        string aseVersion = GetVersion(version == VersionType.Release ? this.aseVersion : AseVersion.v1_5n);
        int playerCount = playerNames.Count;
        string strPlayerCount = playerCount + "/" + this.configuration.MaxPlayerCount;
        string buildType = $"{(byte)version} ";
        string buildNumber = $"0";
        string pingStatus = new('P', 32);
        string strNetRoute = new('N', 32);
        string strUpTime = $"{(int)this.mtaServer.Uptime.Ticks / TimeSpan.TicksPerSecond}";
        string strHttpPort = this.configuration.HttpPort.ToString();
        uint extraDataLength = (uint)(strPlayerCount.Length + buildType.Length + buildNumber.Length + pingStatus.Length + strNetRoute.Length + strUpTime.Length + strHttpPort.Length) + 7;

        bw.Write("EYE2".AsSpan());
        bw.WriteWithLength("mta");
        bw.WriteWithLength(port);
        bw.WriteWithLength(this.configuration.ServerName);
        bw.WriteWithLength(this.mtaServer.GameType);

        bw.Write((byte)(this.mtaServer.MapName.Length + 1 + extraDataLength));
        bw.Write(this.mtaServer.MapName.AsSpan());

        bw.Write((byte)0);
        bw.Write(strPlayerCount.AsSpan());
        bw.Write((byte)0);
        bw.Write(buildType.AsSpan());
        bw.Write((byte)0);
        bw.Write(buildNumber.AsSpan());
        bw.Write((byte)0);
        bw.Write(pingStatus.AsSpan());
        bw.Write((byte)0);
        bw.Write(strNetRoute.AsSpan());
        bw.Write((byte)0);
        bw.Write(strUpTime.AsSpan());
        bw.Write((byte)0);
        bw.Write(strHttpPort.AsSpan());
        bw.WriteWithLength(aseVersion);
        bw.Write((byte)(this.mtaServer.HasPassword ? 1 : 0));
        bw.Write((byte)0); // serial verification
        bw.Write((byte)Math.Min(playerCount, 255));
        bw.Write((byte)Math.Min(this.configuration.MaxPlayerCount, (ushort)255));

        int bytesLeft = (1340 - (int)bw.BaseStream.Position);
        int playersLeftNum = playerNames.Count + 1;
        foreach (string name in playerNames)
        {
            if (bytesLeft - name.Length + 2 > 0)
            {
                bw.WriteWithLength(name);
                bytesLeft -= name.Length + 2;
                playersLeftNum--;
            } else
            {
                string playersLeft = $"And {playersLeftNum} more";
                bw.WriteWithLength(playersLeft);
                break;
            }
        }
        bw.Flush();
        return stream.ToArray();
    }

    public string GetVersion(AseVersion version = AseVersion.v1_5)
    {
        return version switch
        {
            AseVersion.v1_5 => "1.5",
            AseVersion.v1_5n => "1.5n",
            _ => throw new NotImplementedException(this.aseVersion.ToString()),
        };
    }
}
