﻿using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Throttlers;
using AutoDuty.IPC;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoDuty.Helpers
{
    internal static class ObjectHelper
    {
        internal static List<IGameObject>? GetObjectsByObjectKind(ObjectKind objectKind) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.ObjectKind == objectKind)];

        internal static IGameObject? GetObjectByObjectKind(ObjectKind objectKind) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.ObjectKind == objectKind);

        internal static List<IGameObject>? GetObjectsByRadius(float radius) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => GetDistanceToPlayer(o) <= radius)];

        internal static IGameObject? GetObjectByRadius(float radius) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => GetDistanceToPlayer(o) <= radius);

        internal static List<IGameObject>? GetObjectsByName(string name) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.Name.TextValue.Equals(name, StringComparison.CurrentCultureIgnoreCase))];

        internal static IGameObject? GetObjectByName(string name) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.Name.TextValue.Equals(name, StringComparison.CurrentCultureIgnoreCase));

        internal static IGameObject? GetObjectByDataId(uint id) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.DataId == id);

        internal static List<IGameObject>? GetObjectsByPartialName(string name) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.Name.TextValue.Contains(name, StringComparison.CurrentCultureIgnoreCase))];

        internal static IGameObject? GetObjectByPartialName(string name) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.Name.TextValue.Contains(name, StringComparison.CurrentCultureIgnoreCase));

        internal static List<IGameObject>? GetObjectsByNameAndRadius(string objectName) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(g => g.Name.TextValue.Equals(objectName, StringComparison.CurrentCultureIgnoreCase) && Vector3.Distance(Player.Object.Position, g.Position) <= 10)];

        internal static IGameObject? GetObjectByNameAndRadius(string objectName) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(g => g.Name.TextValue.Equals(objectName, StringComparison.CurrentCultureIgnoreCase) && Vector3.Distance(Player.Object.Position, g.Position) <= 10);

        internal static IBattleChara? GetBossObject(int radius = 100) => GetObjectsByRadius(radius)?.OfType<IBattleChara>().FirstOrDefault(b => IsBossFromIcon(b) || BossMod_IPCSubscriber.HasModuleByDataId(b.DataId));

        internal unsafe static float GetDistanceToPlayer(IGameObject gameObject) => GetDistanceToPlayer(gameObject.Position);

        internal unsafe static float GetDistanceToPlayer(Vector3 v3) => Vector3.Distance(v3, Player.GameObject->Position);

        internal unsafe static IGameObject? GetPartyMemberFromRole(string role)
        {
            if (Player.Object != null && GetJobRole(Player.Object.ClassJob.GameData!).ToString().Contains(role, StringComparison.InvariantCultureIgnoreCase))
                return Player.Object;
            else if (Svc.Party.PartyId != 0)
                return Svc.Party.Where(x => GetJobRole(x.ClassJob.GameData!).ToString().Contains(role, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault()?.GameObject;
            else
            {
                var buddies = UIState.Instance()->Buddy.BattleBuddies.ToArray().Where(x => x.DataId != 0);
                foreach (var buddy in buddies)
                {
                    var gameObject = Svc.Objects.FirstOrDefault(x => x.EntityId == buddy.EntityId);

                    if (gameObject == null) continue;

                    var classJob = ((ICharacter)gameObject).ClassJob.GameData;

                    if (classJob == null) continue;

                    if (GetJobRole(classJob).ToString().Contains(role, StringComparison.InvariantCultureIgnoreCase))
                        return gameObject;
                }
            }
            return null;
        }

        internal unsafe static IGameObject? GetTankPartyMember() => GetPartyMemberFromRole("Tank");

        internal unsafe static IGameObject? GetHealerPartyMember() => GetPartyMemberFromRole("Healer");

        //RotationSolver
        internal unsafe static float GetBattleDistanceToPlayer(IGameObject gameObject)
        {
            if (gameObject == null) return float.MaxValue;
            var player = Player.Object;
            if (player == null) return float.MaxValue;

            var distance = Vector3.Distance(player.Position, gameObject.Position) - player.HitboxRadius;
            distance -= gameObject.HitboxRadius;
            return distance;
        }

        internal static BNpcBase? GetObjectNPC(IGameObject gameObject) => Svc.Data.GetExcelSheet<BNpcBase>()?.GetRow(gameObject.DataId) ?? null;

        //From RotationSolver
        internal static bool IsBossFromIcon(IGameObject gameObject) => GetObjectNPC(gameObject)?.Rank is 1 or 2 or 6;

        internal unsafe static uint GrandCompanyTerritoryType(uint grandCompany) => grandCompany == 1 ? 128u : (grandCompany == 2 ? 132u : 130u);

        internal unsafe static uint GrandCompany => UIState.Instance()->PlayerState.GrandCompany;

        internal unsafe static uint GrandCompanyRank => UIState.Instance()->PlayerState.GetGrandCompanyRank();

        internal static float JobRange
        {
            get
            {
                float radius = 25;
                if (!Player.Available) return radius;
                switch (Svc.Data.GetExcelSheet<ClassJob>()?.GetRow(
                    Player.Object.ClassJob.Id)?.GetJobRole() ?? JobRole.None)
                {
                    case JobRole.Tank:
                    case JobRole.Melee:
                        radius = 2.6f;
                        break;
                }
                return radius;
            }
        }

        internal static float AoEJobRange
        {
            get
            {
                float radius = 10;
                if (!Player.Available) return radius;
                switch (Svc.Data.GetExcelSheet<ClassJob>()?.GetRow(
                    Player.Object.ClassJob.Id)?.GetJobRole() ?? JobRole.None)
                {
                    case JobRole.Tank:
                    case JobRole.Melee:
                        radius = 2.6f;
                        break;
                }
                if (Player.Object.ClassJob.Id == 38)
                    radius = 3;
                return radius;
            }
        }

        internal static JobRole GetJobRole(this ClassJob job)
        {
            var role = (JobRole)job.Role;

            if (role is JobRole.Ranged or JobRole.None)
            {
                role = job.ClassJobCategory.Row switch
                {
                    30 => JobRole.RangedPhysical,
                    31 => JobRole.RangedMagical,
                    32 => JobRole.DiscipleOfTheLand,
                    33 => JobRole.DiscipleOfTheHand,
                    _ => JobRole.None,
                };
            }
            return role;
        }

        /// <summary>
        /// The role of jobs.
        /// </summary>
        internal enum JobRole : byte
        {
            None = 0,
            Tank = 1,
            Melee = 2,
            Ranged = 3,
            Healer = 4,
            RangedPhysical = 5,
            RangedMagical = 6,
            DiscipleOfTheLand = 7,
            DiscipleOfTheHand = 8,
        }
        internal enum ClassJobType : uint
        {
            Adventurer = 0,
            Gladiator = 1,
            Pugilist = 2,
            Marauder = 3,
            Lancer = 4,
            Archer = 5,
            Conjurer = 6,
            Thaumaturge = 7,
            Carpenter = 8,
            Blacksmith = 9,
            Armorer = 10,
            Goldsmith = 11,
            Leatherworker = 12,
            Weaver = 13,
            Alchemist = 14,
            Culinarian = 15,
            Miner = 16,
            Botanist = 17,
            Fisher = 18,
            Paladin = 19,
            Monk = 20,
            Warrior = 21,
            Dragoon = 22,
            Bard = 23,
            WhiteMage = 24,
            BlackMage = 25,
            Arcanist = 26,
            Summoner = 27,
            Scholar = 28,
            Rogue = 29,
            Ninja = 30,
            Machinist = 31,
            DarkKnight = 32,
            Astralogian = 33,
            Astrologian = 33,
            Samurai = 34,
            RedMage = 35,
            BlueMage = 36,
            Gunbreaker = 37,
            Dancer = 38,
            Reaper = 39,
            Sage = 40,
        }

        internal static unsafe bool IsValid => Svc.Condition.Any()
        && !Svc.Condition[ConditionFlag.BetweenAreas]
        && !Svc.Condition[ConditionFlag.BetweenAreas51]
        && Player.Available
        && Player.Interactable;

        internal static bool IsJumping => Svc.Condition.Any()
        && (Svc.Condition[ConditionFlag.Jumping]
        || Svc.Condition[ConditionFlag.Jumping61]);

        internal static unsafe bool IsReady => IsValid && !IsOccupied;

        internal static unsafe bool IsOccupied => GenericHelpers.IsOccupied();

        internal static unsafe bool InCombat(this IBattleChara battleChara) => battleChara.Struct()->Character.InCombat;

        internal static unsafe void InteractWithObject(IGameObject? gameObject, bool face = true)
        {
            try
            {
                if (gameObject == null || !gameObject.IsTargetable) 
                    return;
                if (face) 
                    AutoDuty.Plugin.OverrideCamera.Face(gameObject.Position);
                var gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
                TargetSystem.Instance()->InteractWithObject(gameObjectPointer, true);
            }
            catch (Exception ex)
            {
                Svc.Log.Info($"InteractWithObject: Exception: {ex}");
            }
        }
        internal static unsafe AtkUnitBase* InteractWithObjectUntilAddon(IGameObject? gameObject, string addonName)
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && GenericHelpers.IsAddonReady(addon))
                return addon;

            if (EzThrottler.Throttle("InteractWithObjectUntilAddon"))
                InteractWithObject(gameObject);
            
            return null;
        }

        internal static unsafe bool InteractWithObjectUntilNotValid(IGameObject? gameObject)
        {
            if (gameObject == null || !IsValid)
                return true;

            if (EzThrottler.Throttle("InteractWithObjectUntilNotValid"))
                InteractWithObject(gameObject);
            
            return false;
        }

        internal static unsafe bool InteractWithObjectUntilNotTargetable(IGameObject? gameObject)
        {
            if (gameObject == null || !gameObject.IsTargetable)
                return true;

            if (EzThrottler.Throttle("InteractWithObjectUntilNotTargetable"))
                InteractWithObject(gameObject);

            return false;
        }

        internal static unsafe bool PlayerIsCasting => Player.Character->IsCasting;

        internal static bool PartyValidation()
        {
            if (Svc.Party.Count < 4)
                return false;

            var healer = false;
            var tank = false;
            var dpsCount = 0;

            foreach (var item in Svc.Party)
            {
                switch (item.ClassJob.GameData?.Role)
                {
                    case 1:
                        tank = true;
                        break;
                    case 2:
                    case 3:
                        dpsCount++;
                        break;
                    case 4:
                        healer = true;
                        break;
                    default:
                        break;
                }
            }
            return (tank && healer && dpsCount > 1);
        }
    }
}
