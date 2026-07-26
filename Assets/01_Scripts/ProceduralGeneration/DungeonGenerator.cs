using UnityEngine;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

namespace HuntingGame.ProceduralGeneration
{
	/// <summary>
	/// Single responsibility: orchestrate the overall generation loop (pick an open socket, ask
	/// the placer to fill it, regenerate if the result is too small, close remaining sockets at
	/// the end). Ghost pooling, room/loop/bridge placement, and footprint math live in
	/// DungeonGhostPool, DungeonRoomPlacer and DungeonGeometryUtils respectively.
	/// </summary>
	public class DungeonGenerator : MonoBehaviour
	{
		[Header("Generator Parameter")]
		[SerializeField, Tooltip("All the room that might generate in the dungeon")] private RoomData[] roomLibrary;
		[SerializeField, Tooltip("The entrance room of the dungeon")] private GameObject entrancePrefab;
		[SerializeField, Tooltip("Bigger deepness means bigger dungeon and longer generation time")] private int deepness;
		[SerializeField, Tooltip("Seed used for the generation of the dungeon")] private int currentSeed;
		[SerializeField] private LayerMask roomLayer;
		[SerializeField, Tooltip("Set to true if you want to actively try looping in the dungeon. Might not work depending on room type. Will slow down generation")]
		private bool tryLooping;

		[Header("Loop Parameter")]
		[SerializeField, Tooltip("All the loop that might generate in the dungeon")] private LoopData[] loopLibrary;
		[SerializeField, Range(0, 1), Tooltip("Chance to try spawning a loop instead of a room")] private float loopProbability;

		private List<Socket> openSocket = new List<Socket>();
		private List<Room> spawnedRoom = new List<Room>();

		private readonly DungeonGhostPool ghostPool = new DungeonGhostPool();
		private DungeonRoomPlacer placer;

		[ContextMenu("GenerateDungeon")] //For debug purpose, DO NOT USE
		private void TestGenerateWithSeed() => GenerateWithSeed();

		//Use this to launch dungeon generation with seed
		public void GenerateWithSeed(int seed = 0)
		{
			//Check if seed inputed, if not used already existing
			if (seed == 0)
			{
				//Check if seed already exist, if not generate it
				if (currentSeed == 0)
					currentSeed = (int)DateTime.Now.Ticks;
			}
			//If seed inputed, use it
			else
				currentSeed = seed;

			//Set seed
			Random.InitState(currentSeed);

			//Start dungeon generation
			Generate();
		}

		private void Generate()
		{
			placer = new DungeonRoomPlacer(roomLibrary, loopLibrary, ghostPool, transform, deepness, loopProbability, tryLooping);

			//Clean dungeon
			ClearDungeon();

			//Generate elevator spawn room
			Room spawn = Instantiate(entrancePrefab, Vector3.zero, Quaternion.identity, transform).GetComponent<Room>();
			spawnedRoom.Add(spawn);
			openSocket.AddRange(spawn.sockets);

			//Generate other room
			int safetyBreak = 0;
			while (openSocket.Count > 0 && spawnedRoom.Count < deepness && safetyBreak < 500)
			{
				//Safety break is used to prevent infinite looping, stop after 500 itteration
				safetyBreak++;

				//Get a random socket to generate from
				int socketIndex = Random.Range(0, openSocket.Count);
				Socket currentSocket = openSocket[socketIndex];

				//Check if socket is available, if not skip to next
				if (!currentSocket.isAvailable) continue;

				//If socket available, try placing a room from it
				if (placer.TryPlaceRoom(currentSocket, spawnedRoom, openSocket)) openSocket.RemoveAt(socketIndex);
			}

			//If dungeon is too small, regenerate
			if (spawnedRoom.Count < (float)deepness / 2)
			{
				Debug.LogWarning($"Dungeon too small, generated only {spawnedRoom.Count} rooms. Regenerating !");
				Generate();
				return;
			}

			//Close all still opened door after the dungeon is generated
			FinnishDungeon();
		}

		[ContextMenu("ClearDungeon")]
		private void ClearDungeon()
		{
			//Destroy all room in dungeon
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				GameObject child = transform.GetChild(i).gameObject;
				DestroyImmediate(child);
			}

			//Clear all List
			spawnedRoom.Clear();
			openSocket.Clear();
		}

		[ContextMenu("FinnishDungeon")]
		private void FinnishDungeon()
		{
			//Duplicate array because we can't itterate over an array we are modifying
			List<Socket> remainingSockets = new List<Socket>(openSocket);

			//Check all remaining opened socket
			foreach (var s in remainingSockets)
			{
				//If socket is not available, skip
				if (!s.isAvailable) continue;

				//Try placing a dead, if it worked, skip
				if (placer.TryPlaceEndRoom(s, spawnedRoom, openSocket)) continue;

				//If couldn't place dead end, close the socket with a barricade
				s.CloseSocket();
			}

			//Clear the List
			openSocket.Clear();
		}
	}
}
