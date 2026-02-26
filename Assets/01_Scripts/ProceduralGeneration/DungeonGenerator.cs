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

			//Generate elevator spawn room
			Room spawn = Instantiate(entrancePrefab, Vector3.zero, Quaternion.identity, transform).GetComponent<Room>();
			spawnedRoom.Add(spawn);
			openSocket.AddRange(spawn.sockets);

			//Generate other room
			int safetyBreak = 0;
			while(openSocket.Count > 0 && spawnedRoom.Count < deepness && safetyBreak < 500)
			{
				safetyBreak++;
				//Get a socket to generate from
				Socket currentSocket = openSocket[0];
				openSocket.RemoveAt(0);

				//Check if socket is available, if not skip to next
				if (!currentSocket.isAvailable) continue;

				//If socket available, try placing a room from it
				TryPlaceRoom(currentSocket);
			}

			//Close all still opened door after the dungeon is generated
		}

		private void TryPlaceRoom(Socket targetSocket)
		{
			//Find all room having corresponding socket
			List<RoomData> correspondingRooms = roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)).ToList();

			//Test all corresponding room till one fits well with orientation
			bool roomFound = false;
			while (correspondingRooms.Count > 0 && !roomFound)
			{
				//Get a random corresponding room to test
				int roomIndex = Random.Range(0, correspondingRooms.Count);
				GameObject ghost = Instantiate(correspondingRooms[roomIndex].roomPrefab);
				Room ghostRoom = ghost.GetComponent<Room>();
				correspondingRooms.RemoveAt(roomIndex);

				//Find if room can be placed with one socket
				Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom);

				//Check if no good socket found on this room -> the room cannot be placed
				if (incomingSocket == null)
				{
					//Destroy the room we were testing then retry the process
					Destroy(ghost);
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
				if (IsOverlapping(room)) continue;

				//If socket found return it
				return incomingSocket;
			}

			//If no socket found, return null
			return null;
		}

		private void AlignRooms(Socket anchor, Socket incoming, Transform roomTransform)
		{
			Quaternion rotationOffset = Quaternion.FromToRotation(incoming.transform.forward, -anchor.transform.forward);
			roomTransform.rotation = rotationOffset * roomTransform.rotation;

			Vector3 positionOffset = anchor.transform.position - incoming.transform.position;
			roomTransform.position += positionOffset;
		}

		private bool IsOverlapping(Room room)
		{
			Bounds b = room.boundCollider.bounds;
			Collider[] colliders = Physics.OverlapBox(b.center, b.extents * .9f, room.transform.rotation, roomLayer);
			foreach (var c in colliders)
				if (c.transform.root != room.transform.root) return true;
			return false;
		}
	}
}
