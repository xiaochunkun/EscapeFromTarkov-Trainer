﻿using System.Collections.Generic;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.UI;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features
{
	internal abstract class BaseMapToggleFeature : ToggleFeature
	{
		protected enum HostileType
		{
			Scav,
			ScavRaider,
			Boss,
			Cultist,
			Bear,
			Usec,
		}

		[ConfigurationProperty(Order = 40)] 
		public bool ShowPlayers { get; set; } = true;

		[ConfigurationProperty(Order = 50)]
		public bool ShowScavs { get; set; } = true;

		[ConfigurationProperty(Order = 60)]
		public bool ShowScavRaiders { get; set; } = true;

		[ConfigurationProperty(Order = 70)]
		public bool ShowBosses { get; set; } = true;

		[ConfigurationProperty(Order = 80)]
		public bool ShowCultists { get; set; } = true;

		private GameObject? _mapCameraObject = null;
		private Camera? _mapCamera = null;

		protected void ToggleMapCameraIfNeeded(bool state)
		{
			if (_mapCamera == null)
				return;

			if (_mapCamera.enabled == state)
				return;

			_mapCamera.enabled = state;
		}

		protected void SetupMapCameraOnce(Camera camera, float x, float y, float sizex, float sizey)
		{
			if (_mapCameraObject != null)
			{
				ToggleMapCameraIfNeeded(true);
				return;
			}

			// We need to setup weather for proper rendering
			Weather.ToClearWeather();

			_mapCameraObject = new GameObject(GetType().FullName + nameof(_mapCameraObject), typeof(Camera), typeof(PrismEffects));
			_mapCameraObject.GetComponent<PrismEffects>().CopyComponentValues(camera.GetComponent<PrismEffects>());
			_mapCamera = _mapCameraObject.GetComponent<Camera>();
			_mapCamera.name = GetType().FullName + nameof(_mapCamera);
			_mapCamera.pixelRect = new Rect(x, y, sizex, sizey);
			_mapCamera.allowHDR = false;
			_mapCamera.depth = -1;

			// Prevent NullReferenceException in PrismEffects 
			GameWorld.OnDispose -= UpdateWhenDisabled;
			GameWorld.OnDispose += UpdateWhenDisabled;
		}

		protected void UpdateMapCamera(Camera camera, float range)
		{
			if (_mapCameraObject == null) 
				return;

			var cameraTransform = camera.transform;

			var mapTransform = _mapCameraObject.transform;
			mapTransform.eulerAngles = new Vector3(90, cameraTransform.eulerAngles.y, cameraTransform.eulerAngles.z);
			mapTransform.localPosition = new Vector3(cameraTransform.localPosition.x, range * Mathf.Tan(45), cameraTransform.localPosition.z);
		}

		protected void DrawHostiles(Camera camera, IEnumerable<Player> hostiles, float x, float y, float sizex, float sizey, float range)
		{
			var cameraTransform = camera.transform;
			var feature = FeatureFactory.GetFeature<Players>();
			if (feature == null)
				return;

			foreach (var enemy in hostiles)
			{
				if (!enemy.IsValid())
					continue;

				var position = enemy.Transform.position;

				var distance = Mathf.Round(Vector3.Distance(cameraTransform.position, position));
				if (range > 0 && distance > range)
					continue;

				var hostileType = GetHostileType(enemy);

				switch (hostileType)
				{
					case HostileType.Scav when !ShowScavs:
					case HostileType.ScavRaider when !ShowScavRaiders:
					case HostileType.Cultist when !ShowCultists:
					case HostileType.Boss when !ShowBosses:
					case HostileType.Bear or HostileType.Usec when !ShowPlayers:
						continue;

					default:
					{
						var playerColor = feature.GetPlayerColors(enemy);
						DrawEnemy(camera, enemy, x, y, sizex, sizey, range, playerColor.Color);
						break;
					}
				}
			}
		}

		private static HostileType GetHostileType(Player player)
		{
			var info = player.Profile?.Info;
			if (info == null)
				return HostileType.Scav;

			var settings = info.Settings;
			if (settings != null)
			{
				switch (settings.Role)
				{
					case WildSpawnType.pmcBot:
						return HostileType.ScavRaider;
					case WildSpawnType.sectantWarrior:
						return HostileType.Cultist;
				}

				if (settings.IsBoss())
					return HostileType.Boss;
			}

			return info.Side switch
			{
				EPlayerSide.Bear => HostileType.Bear,
				EPlayerSide.Usec => HostileType.Usec,
				_ => HostileType.Scav
			};
		}

		protected static void DrawEnemy(Camera camera, Player enemy, float x, float y, float sizex, float sizey, float range, Color playerColor)
		{
			var cameraTransform = camera.transform;
			var cameraPosition = cameraTransform.position;

			var enemyPosition = enemy.Transform.position;
			var cameraEulerY = cameraTransform.eulerAngles.y;

			var enemyMap = FindMapPoint(cameraPosition, enemyPosition, cameraEulerY, x, y, sizex, sizey, range);

			var enemyLookDirection = enemy.LookDirection;

			var enemyOffset = enemyPosition + enemyLookDirection * 8f;
			var playerRealRight = enemy.MovementContext.PlayerRealRight;

			var enemyOffset2 = enemyPosition + enemyLookDirection * 4f + playerRealRight * 2f;
			var enemyOffset3 = enemyPosition + enemyLookDirection * 4f - playerRealRight * 2f;

			var enemyForward = FindMapPoint(cameraPosition, enemyOffset, cameraEulerY, x, y, sizex, sizey, range);
			var enemyArrow = FindMapPoint(cameraPosition, enemyOffset2, cameraEulerY, x, y, sizex, sizey, range);
			var enemyArrow2 = FindMapPoint(cameraPosition, enemyOffset3, cameraEulerY, x, y, sizex, sizey, range);

			Render.DrawLine(enemyMap, enemyForward, 2f, Color.white);
			Render.DrawLine(enemyArrow, enemyForward, 2f, Color.white);
			Render.DrawLine(enemyArrow2, enemyForward, 2f, Color.white);
			Render.DrawCircle(enemyMap, 10f, playerColor, 2f, 8);
		}

		private static Vector2 FindMapPoint(Vector3 playerPosition, Vector3 enemyPosition, float playerEulerY, float x, float y, float sizex, float sizey, float range)
		{
			float enemyY = playerPosition.x - enemyPosition.x;
			float enemyX = playerPosition.z - enemyPosition.z;
			float enemyAtan = Mathf.Atan2(enemyY, enemyX) * Mathf.Rad2Deg - 270 - playerEulerY;

			var enemyDistance = Mathf.Round(Vector3.Distance(playerPosition, enemyPosition));

			float enemyMapX = enemyDistance * Mathf.Cos(enemyAtan * Mathf.Deg2Rad);
			float enemyMapY = enemyDistance * Mathf.Sin(enemyAtan * Mathf.Deg2Rad);

			enemyMapX = enemyMapX * (sizex / range) / 2f;
			enemyMapY = enemyMapY * (sizey / range) / 2f;

			return new Vector2(x + sizex / 2f + enemyMapX, y + sizey / 2f + enemyMapY);
		}
	}
}
