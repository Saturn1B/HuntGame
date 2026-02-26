using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProceduralGeneration
{
	public class DungeonGenerator : MonoBehaviour
	{
		[SerializeField] private RoomData[] roomLibrary;
		[SerializeField] private GameObject entrancePrefab;
		[SerializeField] private int deepness;
		[SerializeField] private LayerMask roomLayer;

		private List<Socket> openSocket = new List<Socket>();
		private List<Room> spawnedRoom = new List<Room>();

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
				safetyBreak++;
				//Get a socket to generate from
				int socketIndex = Random.Range(0, openSocket.Count);
				Socket currentSocket = openSocket[socketIndex];

				//Check if socket is available, if not skip to next
				if (!currentSocket.isAvailable) continue;

				//If socket available, try placing a room from it
				if(TryPlaceRoom(currentSocket)) openSocket.RemoveAt(socketIndex);
			}

			//Close all still opened door after the dungeon is generated
			FinnishDungeon();
		}

		private bool TryPlaceRoom(Socket targetSocket)
		{
			//Find all room having corresponding socket
			List<RoomData> correspondingRooms = roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)).ToList();

			if (spawnedRoom.Count < deepness * .75f)
				correspondingRooms = correspondingRooms.Where(r => r.roomPrefab.GetComponent<Room>().sockets.Count > 1).ToList();

			//Test all corresponding room till one fits well with orientation
			bool roomFound = false;
			while (correspondingRooms.Count > 0 && !roomFound)
			{
				//Get a random corresponding room to test
				RoomData selectedData = GetWeightedRandomRoom(correspondingRooms);
				GameObject ghost = Instantiate(selectedData.roomPrefab);
				Room ghostRoom = ghost.GetComponent<Room>();
				correspondingRooms.Remove(selectedData);

				//Find if room can be placed with one socket
				Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom);

				//Check if no good socket found on this room -> the room cannot be placed
				if (incomingSocket == null)
				{
					//Destroy the room we were testing then retry the process
					DestroyImmediate(ghost);
					Physics.SyncTransforms();
					continue;
				}

				//If good socket found -> the room can be placed, stop search there
				roomFound = true;

				//Validate the room and socket
				targetSocket.isAvailable = false;
				incomingSocket.isAvailable = false;
				spawnedRoom.Add(ghostRoom);
				openSocket.AddRange(ghostRoom.sockets);
				ghost.transform.SetParent(this.transform);
			}

			return roomFound;
		}

		private Socket CheckRoomValidityWithSocket(Socket targetSocket, Room room)
		{
			//Find all corresponding sockets in the room socket array
			List<Socket> correspondingSockets = room.sockets.Where(sck => sck.socketType == targetSocket.socketType).ToList();

			//Test all corresponding socket till one fits well with orientation
			while (correspondingSockets.Count > 0)
			{
				//Get a random corresponding socket to test
				int socketIndex = Random.Range(0, correspondingSockets.Count);
				Socket incomingSocket = correspondingSockets[socketIndex];
				correspondingSockets.RemoveAt(socketIndex);

				//Align room to target door
				AlignRooms(targetSocket, incomingSocket, room.transform);

				//Check for room averlaping
				if (IsOverlapping(room, targetSocket)) continue;

				//If socket found return it
				return incomingSocket;
			}

			//If no socket found, return null
			return null;
		}

		private RoomData GetWeightedRandomRoom(List<RoomData> options)
		{
			int totalWeight = options.Sum(r => r.roomWeight);
			int randomValue = Random.Range(0, totalWeight);
			int currentSum = 0;

			foreach (var room in options)
			{
				currentSum += room.roomWeight;
				if(randomValue < currentSum)
				{
					return room;
				}
			}

			return options[0];
		}

		private void AlignRooms(Socket anchor, Socket incoming, Transform roomTransform)
		{
			Quaternion targetSocketRot = Quaternion.LookRotation(-anchor.transform.forward, anchor.transform.up);

			Quaternion rotationOffset = targetSocketRot * Quaternion.Inverse(incoming.transform.localRotation);
			roomTransform.rotation = rotationOffset;

			Vector3 targetSocketPos = anchor.transform.position + (anchor.transform.forward * .5f);

			Vector3 positionOffset = targetSocketPos - incoming.transform.position;
			roomTransform.position += positionOffset;

			Physics.SyncTransforms();
		}

		private bool IsOverlapping(Room room, Socket targetSocket)
		{
			float padding = .15f;
			Bounds b = room.boundCollider.bounds;

			Collider[] colliders = Physics.OverlapBox(b.center, (b.extents - Vector3.one * padding), room.transform.rotation, roomLayer);

			foreach (var c in colliders)
			{
				Room hitRoom = c.transform.GetComponentInParent<Room>();

				if (hitRoom == null) continue;

				if (hitRoom.gameObject == room.gameObject) continue;

				if (hitRoom == targetSocket.room) continue;

				return true;
			}
			return false;
		}

		[ContextMenu("ClearDungeon")]
		private void ClearDungeon()
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				GameObject child = transform.GetChild(i).gameObject;
				DestroyImmediate(child);
			}

			spawnedRoom.Clear();
			openSocket.Clear();

			Physics.SyncTransforms();
		}

		[ContextMenu("FinnishDungeon")]
		private void FinnishDungeon()
		{
			List<Socket> remainingSockets = new List<Socket>(openSocket);

			foreach (var s in remainingSockets)
			{
				if (!s.isAvailable) continue;

				if (TryPlaceEndRoom(s))
				{
					continue;
				}

				s.CloseSocket();
			}

			openSocket.Clear();
		}

		private bool TryPlaceEndRoom(Socket targetSocket)
		{
			//Find all room having corresponding socket
			List<RoomData> correspondingRooms = roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)
													&& obj.roomPrefab.GetComponent<Room>().sockets.Count == 1).ToList();

			//Test all corresponding room till one fits well with orientation
			bool roomFound = false;
			while (correspondingRooms.Count > 0 && !roomFound)
			{
				//Get a random corresponding room to test
				RoomData selectedData = GetWeightedRandomRoom(correspondingRooms);
				GameObject ghost = Instantiate(selectedData.roomPrefab);
				Room ghostRoom = ghost.GetComponent<Room>();
				correspondingRooms.Remove(selectedData);

				//Find if room can be placed with one socket
				Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom);

				//Check if no good socket found on this room -> the room cannot be placed
				if (incomingSocket == null)
				{
					//Destroy the room we were testing then retry the process
					DestroyImmediate(ghost);
					Physics.SyncTransforms();
					continue;
				}

				//If good socket found -> the room can be placed, stop search there
				roomFound = true;

				//Validate the room and socket
				targetSocket.isAvailable = false;
				incomingSocket.isAvailable = false;
				spawnedRoom.Add(ghostRoom);
				ghost.transform.SetParent(this.transform);
			}

			if (!roomFound) Debug.DrawRay(targetSocket.transform.position, Vector3.up * 10, Color.yellow, 5);
			else Debug.DrawRay(targetSocket.transform.position, Vector3.up * 10, Color.white, 5);

			return roomFound;
		}
	}
}
