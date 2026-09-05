using System.Collections.Generic;
using NetworkPrototype.Topic04.Player;
using UnityEngine;
using UnityEngine.UI;

namespace NetworkPrototype.Topic04.UI
{
    [DisallowMultipleComponent]
    public sealed class Topic04ScoreboardUI : MonoBehaviour
    {
        [SerializeField] private Text scoreText;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

        private readonly List<NetworkPlayer2DController> orderedPlayers = new(4);
        private float nextRefreshTime;

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshScoreboard();
        }

        private void RefreshScoreboard()
        {
            if (scoreText == null)
            {
                return;
            }

            orderedPlayers.Clear();
            foreach (NetworkPlayer2DController player in NetworkPlayer2DController.ActivePlayers)
            {
                if (player != null && player.IsSpawned)
                {
                    orderedPlayers.Add(player);
                }
            }

            orderedPlayers.Sort((left, right) => left.OwnerClientId.CompareTo(right.OwnerClientId));

            if (orderedPlayers.Count == 0)
            {
                scoreText.text = "PLACAR\nAguardando jogadores...";
                return;
            }

            var builder = new System.Text.StringBuilder("PLACAR");
            foreach (NetworkPlayer2DController player in orderedPlayers)
            {
                builder.Append("\nP").Append(player.OwnerClientId + 1).Append(": ").Append(player.Score);
                if (!player.IsAlive)
                {
                    builder.Append(" (respawn)");
                }
            }

            scoreText.text = builder.ToString();
        }
    }
}
