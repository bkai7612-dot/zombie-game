using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Automated end-to-end check of the single-player loop through the real authority code paths:
    /// start host, build map, start match, zombies spawn, kill rewards, range-checked purchases (door, wall buy,
    /// supply drop, escape funding) and defeat. Run a build with <c>-uzssmoketest</c> (optionally with
    /// <c>-batchmode -nographics</c>); the process exits with code 0 on success and writes smoketest.log.
    /// </summary>
    public sealed class SmokeTest : MonoBehaviour
    {
        private readonly StringBuilder log = new StringBuilder();
        private int failures;

        public bool Finished { get; private set; }
        public int Failures => failures;
        public string Report => log.ToString();

        private void Start() => StartCoroutine(Run());

        private void Check(bool condition, string what)
        {
            log.AppendLine((condition ? "PASS " : "FAIL ") + what);
            if (!condition) failures++;
            Debug.Log("[UZS-SMOKE] " + (condition ? "PASS " : "FAIL ") + what);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Run()
        {
            var cc = ConnectionController.Instance;
            Check(cc != null && cc.StartSinglePlayer(), "single-player host starts");
            yield return WaitFor(() => MatchState.Instance != null && PlayerAvatar.Local != null, 15f);
            var match = MatchState.Instance;
            var me = PlayerAvatar.Local;
            Check(match != null && me != null, "match object and local player spawned");
            if (match == null || me == null)
            {
                Finish();
                yield break;
            }

            int interactables = 0;
            foreach (var unused in Interactable.All) interactables++;
            Check(MapBuilder.Current != null, "map built");
            Check(interactables == MapCatalog.Doors.Length + MapCatalog.Stations.Length + 1, $"all interactables registered ({interactables})");

            match.ServerStartMatch();
            yield return null;
            Check(match.Phase == MatchPhase.Playing, "match started");
            Check(me.Points == GameInfo.StartingPoints, "starting points");
            Check(me.Weapons.View.Active.Weapon == WeaponId.Pistol, "starter pistol");

            yield return WaitFor(() => ZombieAgent.All.Count > 0, 20f);
            Check(ZombieAgent.All.Count > 0, "round 1 zombies spawned");
            Check(match.Round == 1, "round counter is 1");

            if (ZombieAgent.All.Count > 0)
            {
                int before = me.Points;
                bool killed = ZombieAgent.All[0].ServerTakeDamage(1e6f, me, true, false);
                Check(killed && me.Points == before + PointsRules.KillRewardFor(true, false), "headshot kill reward");
            }
            // Keep the remaining checks deterministic: no more spawns, clear the street.
            match.Director.PauseSpawning = true;
            foreach (var z in ZombieAgent.All.ToArray()) z.ServerTakeDamage(1e6f, null, false, false);

            // Door: refused from far away, accepted up close, cannot be bought twice.
            me.ServerAddPoints(20000);
            var door = MapCatalog.Doors[0];
            me.Motor.Teleport(Geo.V(MapCatalog.PlayerSpawns[0]), 0f);
            yield return null;
            Check(!me.Interactor.ServerTryInteract(door.Id, out var msg) && msg == "Too far away", "door refused out of range");
            me.Motor.Teleport(Geo.V(door.Position) + new Vector3(0f, 0.1f, -1.5f), 0f);
            yield return null;
            int pts = me.Points;
            Check(me.Interactor.ServerTryInteract(door.Id, out _), "door purchase accepted in range");
            Check((match.DoorMask & 1UL) != 0 && me.Points == pts - door.Cost, "door opened and charged once");
            Check(!me.Interactor.ServerTryInteract(door.Id, out _), "open door cannot be bought again");

            // Wall buy.
            var smg = MapCatalog.FindStation(10);
            me.Motor.Teleport(Geo.V(smg.Position) + new Vector3(0f, -1.2f, 1.0f), 180f);
            yield return null;
            Check(me.Interactor.ServerTryInteract(smg.Id, out _) && me.Weapons.View.Owns(WeaponId.Smg), "SMG wall buy");

            // Supply drop.
            var boxDef = MapCatalog.BoxLocations[match.BoxLocation];
            me.Motor.Teleport(Geo.V(boxDef.Position) + Quaternion.Euler(0f, boxDef.Yaw, 0f) * Vector3.forward * 0.8f, 0f);
            yield return null;
            Check(me.Interactor.ServerTryInteract(MapCatalog.MysteryBoxInteractableId, out _) && match.BoxState == BoxState.Rolling, "supply drop rolls");
            yield return WaitFor(() => match.BoxState != BoxState.Rolling, 6f);
            if (match.BoxState == BoxState.Offering)
            {
                var offered = match.BoxOfferWeapon;
                Check(me.Interactor.ServerTryInteract(MapCatalog.MysteryBoxInteractableId, out _) && me.Weapons.View.Owns(offered), "supply drop weapon taken");
            }
            else
            {
                Check(match.BoxState == BoxState.Moving, "supply drop relocated (refund path)");
            }

            // Escape funding.
            var console = MapCatalog.FindStation(MapCatalog.EscapeConsoleInteractableId);
            me.ServerAddPoints(match.EscapeCost);
            me.Motor.Teleport(Geo.V(console.Position) + new Vector3(0f, 0.1f, -1.2f), 0f);
            yield return null;
            Check(me.Interactor.ServerTryInteract(console.Id, out _) && match.EscapeActive, "UFO escape funded and countdown started");

            // Defeat: the only player goes down.
            me.ServerTakeDamage(1e6f, me.transform.position);
            yield return null;
            yield return null;
            Check(me.Life == LifeState.Downed, "player downed at 0 health");
            Check(match.Phase == MatchPhase.Defeat, "solo down ends the match");

            Finish();
        }

        private void Finish()
        {
            string summary = failures == 0 ? "SMOKE TEST PASSED" : $"SMOKE TEST FAILED ({failures} failures)";
            log.AppendLine(summary);
            Debug.Log("[UZS-SMOKE] " + summary);
            Finished = true;
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "smoketest.log"), log.ToString());
            }
            catch (IOException e)
            {
                Debug.LogWarning("[UZS-SMOKE] could not write log: " + e.Message);
            }
            if (ConnectionController.Instance != null) ConnectionController.Instance.Leave();
            if (!Application.isEditor) Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
