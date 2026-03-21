using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProceduralGeneration
{
	public class DungeonGenerator : MonoBehaviour
	{
		[Header("Generator Parameter")]
		[SerializeField, Tooltip("All the room that might generate in the dungeon")] private RoomData[] roomLibrary;
		[SerializeField, Tooltip("The entrance room of the dungeon")] private GameObject entrancePrefab;
		[SerializeField, Tooltip("Bigger deepness means bigger dungeon and longer generation time")] private int deepness;
		[SerializeField] private LayerMask roomLayer;
		[SerializeField, Tooltip("Set to true if you want to actively try looping in the dungeon. Might not work depending on room type. Will slow down generation")]
		private bool tryLooping;

		private List<Socket> openSocket = new List<Socket>();
		private List<Room> spawnedRoom = new List<Room>();

		private Dictionary<RoomData, Room> ghostPool = new Dictionary<RoomData, Room>();

		private void SetupGhostPool()
		{
			if (ghostPool.Count > 0) return;

			foreach (var data in roomLibrary)
			{
				GameObject ghostObj = Instantiate(data.roomPrefab);
				ghostObj.name = $"Ghost_{data.name}";
				ghostObj.SetActive(false);

				Room ghostRoom = ghostObj.GetComponent<Room>();

				ghostPool.Add(data, ghostRoom);
			}
		}

		private Room GetGhost(RoomData data)
		{
			Room ghost = ghostPool[data];
			ghost.gameObject.SetActive(true);
			return ghost;
		}

		[ContextMenu("GenerateDungeon")]
		private void Generate()
		{
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
				if (TryPlaceRoom(currentSocket)) openSocket.RemoveAt(socketIndex);
			}

			//If dungeon is too small, regenerate
			if(spawnedRoom.Count < (float)deepness / 2)
			{
				Debug.LogWarning($"Dungeon too small, generated only {spawnedRoom.Count} rooms. Regenerating !");
				Generate();
				return;
			}

			//Close all still opened door after the dungeon is generated
			FinnishDungeon();
		}

		private bool TryPlaceRoom(Socket targetSocket)
		{
			SetupGhostPool(); //Ensure ghost pool is set

			//Check if we want to actively try looping
			if (tryLooping)
			{
				//Before generating a room, we check to see if we can't bridge two existing socket
				//Check on all open socket
				foreach (Socket otherSocket in openSocket.ToList())
				{
					//if the socket is not available or it's from the same room as the other sockt we're trying to bridge with, we skip
					if (!otherSocket.isAvailable || otherSocket.room == targetSocket.room) continue;

					//get the distance between the two socket
					float dist = Vector3.Distance(targetSocket.transform.position, otherSocket.transform.position);

					//if the two socket are in acceptable range, try bridging between them
					if (dist > 1f && dist < 20)
						if (TryBridgeRoom(targetSocket, otherSocket)) return true;
				}
			}

			//Find all room having corresponding socket
			List<RoomData> correspondingRooms = roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)).ToList();

			//If room count is bellow a certain number, remove all dead end from coresponding rooms
			if (spawnedRoom.Count < deepness * .75f)
				correspondingRooms = correspondingRooms.Where(r => r.roomPrefab.GetComponent<Room>().sockets.Length > 1).ToList();

			//Test all corresponding room until one fits well with orientation
			while (correspondingRooms.Count > 0)
			{
				//Get a random corresponding room to test by weight
				RoomData selectedData = GetWeightedRandomRoom(correspondingRooms);
				correspondingRooms.Remove(selectedData);

				Room ghostRoom = GetGhost(selectedData);

				//Find if room can be placed with one socket
				Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom);

				//Check if no good socket found on this room -> the room cannot be placed
				if (incomingSocket == null)
				{
					//Return ghost room to pool
					ghostRoom.gameObject.SetActive(false);
					continue;
				}

				//If good socket found -> the room can be placed, stop search there
				GameObject realRoomObj = Instantiate(selectedData.roomPrefab, transform);
				Room realRoom = realRoomObj.GetComponent<Room>();

				realRoom.transform.position = ghostRoom.transform.position;
				realRoom.transform.rotation = ghostRoom.transform.rotation;

				//Return ghost room to pool
				ghostRoom.gameObject.SetActive(false);

				//Validate the room and socket
				targetSocket.isAvailable = false;

				int socketIndex = System.Array.IndexOf(ghostRoom.sockets, incomingSocket);
				realRoom.sockets[socketIndex].isAvailable = false;

				spawnedRoom.Add(realRoom);
				openSocket.AddRange(realRoom.sockets.Where(s => s.isAvailable));

				foreach (Socket newSocket in realRoom.sockets)
				{
					if (!newSocket.isAvailable) continue;

					foreach (Socket existingSocket in openSocket)
					{
						if (!existingSocket.isAvailable || existingSocket == newSocket) continue;

						if (existingSocket.socketType != newSocket.socketType) continue;

						if (Vector3.Distance(newSocket.socket.transform.position, existingSocket.socket.transform.position) < .1f)
						{
							newSocket.isAvailable = false;
							existingSocket.isAvailable = false;
							Debug.DrawRay(newSocket.transform.position, Vector3.up * 20, Color.cyan, 5);
							Debug.Log($"Natural loop created between {realRoom.name} and {existingSocket.room.name}");
						}
					}
				}

				return true;
			}

			//return the state of our search, did we found a room to place or not
			return false;
		}

		private bool TryBridgeRoom(Socket socketA, Socket socketB)
		{
			SetupGhostPool(); //Ensure ghost pool is set

			//Find all room having having at least 2 socket
			List<RoomData> candidates = roomLibrary.Where(r => r.roomPrefab.GetComponent<Room>().sockets.Length >= 2).ToList();

			//Test all candidates
			foreach (var data in candidates)
			{
				//Spawn the room to test
				Room ghostRoom = GetGhost(data);
				var ghostSockets = ghostRoom.sockets;

				for (int i = 0; i < ghostSockets.Length; i++)
				{
					//if the first socket of the room is not of the same type as the socket we're trying to connect, we skip
					if (ghostSockets[i].socketType != socketA.socketType) continue;

					//Align room to target door
					AlignRooms(socketA, ghostSockets[i], ghostRoom.transform);

					for (int j = 0; j < ghostSockets.Length; j++)
					{
						//if ghost room socket is the same as the one we already tested, or is not of the same type as the socket we're trying to connect, we skip
						if (i == j) continue;
						if (ghostSockets[j].socketType != socketB.socketType) continue;

						float dist = Vector3.Distance(ghostSockets[j].transform.position, socketB.transform.position);
						float angle = Quaternion.Angle(ghostSockets[j].transform.rotation, Quaternion.LookRotation(-socketB.transform.forward, socketB.transform.up));

						//Check if the two socket are superposed
						if(dist < .1f && angle < 1f)
						{
							//Check for room overlaping
							if (!IsOverlapping(ghostRoom, socketA))
							{
								//Bridge is valid, validate the room and socket

								GameObject realRoomObj = Instantiate(data.roomPrefab, transform);
								Room realRoom = realRoomObj.GetComponent<Room>();

								realRoom.transform.position = ghostRoom.transform.position;
								realRoom.transform.rotation = ghostRoom.transform.rotation;

								//Return ghost room to pool
								ghostRoom.gameObject.SetActive(false);

								socketA.isAvailable = false;
								socketB.isAvailable = false;
								realRoom.sockets[i].isAvailable = false;
								realRoom.sockets[j].isAvailable = false;

								spawnedRoom.Add(realRoom);
								openSocket.AddRange(realRoom.sockets.Where(s => s.isAvailable));

								Debug.Log($"<color=cyan>Loop Created!</color> {realRoom.name} connected {socketA.room.name} to {socketB.room.name}");
								//Bridge found, return true
								return true;
							}
						}
					}
				}

				//Room can't bridge, return it to pool
				ghostRoom.gameObject.SetActive(false);
			}

			//No bridge found, return false
			return false;
		}

		private Socket CheckRoomValidityWithSocket(Socket targetSocket, Room room)
		{
			//Find all corresponding sockets in the room socket array
			List<Socket> correspondingSockets = room.sockets.Where(sck => sck.socketType == targetSocket.socketType).ToList();

			//Test all corresponding socket until one fits well with orientation
			while (correspondingSockets.Count > 0)
			{
				//Get a random corresponding socket to test
				int socketIndex = Random.Range(0, correspondingSockets.Count);
				Socket incomingSocket = correspondingSockets[socketIndex];
				correspondingSockets.RemoveAt(socketIndex);

				//Align room to target door
				AlignRooms(targetSocket, incomingSocket, room.transform);

				//Check for room overlaping
				if (IsOverlapping(room, targetSocket)) continue;

				//If socket found return it
				return incomingSocket;
			}

			//If no socket found, return null
			return null;
		}

		private RoomData GetWeightedRandomRoom(List<RoomData> options)
		{
			//Calculate total sum of all weights in options list
			int totalWeight = options.Sum(r => r.roomWeight);

			//Pick random number between 0 and total weight
			int randomValue = Random.Range(0, totalWeight);
			int currentSum = 0;

			//Check all options, adding their weight to a running total
			foreach (var room in options)
			{
				currentSum += room.roomWeight;

				//If random value is within current accumulated weight range, return this room
				if (randomValue < currentSum)
				{
					return room;
				}
			}

			//If no room found, by default, return the first one in the list
			return options[0];
		}

		private void AlignRooms(Socket anchor, Socket incoming, Transform roomTransform)
		{
			//Calculate target rotation, making it face opposite direction of anchor, while keeping the room up
			Quaternion targetSocketRot = Quaternion.LookRotation(-anchor.transform.forward, anchor.transform.up);

			//Calculate rotation offset to know how much our room should rotate reach target socket rotation
			Quaternion rotationOffset = targetSocketRot * Quaternion.Inverse(incoming.transform.localRotation);
			roomTransform.rotation = rotationOffset;

			//Adding gap to target position
			Vector3 targetSocketPos = anchor.transform.position + (anchor.transform.forward * .5f);

			//Calculate position offset an place the room with that offset
			Vector3 positionOffset = anchor.socket.transform.position - incoming.socket.transform.position;
			roomTransform.position += positionOffset;
		}

		private bool IsOverlapping(Room room, Socket targetSocket)
		{
			Physics.SyncTransforms();

			float padding = .05f;
			//Get the bounds or our room
			Bounds b = room.boundCollider.bounds;

			//Get all the object colliding with our room (added a small bit of padding for tolerance)
			Collider[] colliders = Physics.OverlapBox(b.center, (b.extents - Vector3.one * padding), room.transform.rotation, roomLayer);

			//Check on all the room colliding object found
			foreach (var c in colliders)
			{
				Room hitRoom = c.transform.GetComponentInParent<Room>();

				//If doesn't have room script, skip
				if (hitRoom == null) continue;

				//If it's the room we're testing for, skip
				if (hitRoom.gameObject == room.gameObject) continue;

				//If it's the room we're buidling from, skip
				if (hitRoom == targetSocket.room) continue;

				//Else, we're colliding with another room, return overlapping to true
				return true;
			}

			//No overlapping found
			return false;
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
				if (TryPlaceEndRoom(s)) continue;

				//If couldn't place dead end, close the socket with a barricade
				s.CloseSocket();
			}

			//Clear the List
			openSocket.Clear();
		}

		private bool TryPlaceEndRoom(Socket targetSocket)
		{
			SetupGhostPool(); //Ensure ghost pool is set

			//Find all room having corresponding socket that are dead ends
			List<RoomData> correspondingRooms = roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)
													&& obj.roomPrefab.GetComponent<Room>().sockets.Length == 1).ToList();

			//Test all corresponding room until one fits well with orientation
			while (correspondingRooms.Count > 0)
			{
				//Get a random corresponding room to test by weight
				RoomData selectedData = GetWeightedRandomRoom(correspondingRooms);
				correspondingRooms.Remove(selectedData);

				Room ghostRoom = GetGhost(selectedData);

				//Find if room can be placed with one socket
				Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom);

				//Check if no good socket found on this room -> the room cannot be placed
				if (incomingSocket == null)
				{
					//Return ghost room to pool
					ghostRoom.gameObject.SetActive(false);
					continue;
				}

				//If good socket found -> the room can be placed, stop search there
				GameObject realRoomObj = Instantiate(selectedData.roomPrefab, transform);
				Room realRoom = realRoomObj.GetComponent<Room>();

				realRoom.transform.position = ghostRoom.transform.position;
				realRoom.transform.rotation = ghostRoom.transform.rotation;

				//Return ghost room to pool
				ghostRoom.gameObject.SetActive(false);

				//Validate the room and socket
				targetSocket.isAvailable = false;

				int socketIndex = System.Array.IndexOf(ghostRoom.sockets, incomingSocket);
				realRoom.sockets[socketIndex].isAvailable = false;

				spawnedRoom.Add(realRoom);
				openSocket.AddRange(realRoom.sockets.Where(s => s.isAvailable));

				Debug.DrawRay(targetSocket.transform.position, Vector3.up * 10, Color.white, 5);
				return true;
			}

			Debug.DrawRay(targetSocket.transform.position, Vector3.up * 10, Color.yellow, 5);

			//return the state of our search, did we found a room to place or not
			return false;
		}
	}
}
