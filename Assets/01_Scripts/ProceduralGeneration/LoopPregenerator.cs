using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProceduralGeneration
{
	public class LoopPregenerator : MonoBehaviour
	{
		[Header("Loop Pregenerator Parameter")]
		[SerializeField, Tooltip("All the room used in pregeneration")] private RoomData[] roomLibrary;
		[SerializeField, Tooltip("Number of room in a loop"), Range(3, 6)] private int loopSize;
		[SerializeField] private LayerMask roomLayer;

		[SerializeField] private List<Room> currentLoop = new List<Room>();
		[SerializeField] private List<Socket> openSocket = new List<Socket>();

		[ContextMenu("Loop Pregen")]
		async public void LoopPregen()
		{
			//Purge library of mistaken dead end

			roomLibrary = roomLibrary.Where(r => r.roomPrefab.GetComponent<Room>().sockets.Length > 1).ToArray();

			foreach (RoomData data in roomLibrary)
			{
				Room room = Instantiate(data.roomPrefab, Vector3.zero, Quaternion.identity).GetComponent<Room>();
				currentLoop.Add(room);
				openSocket.AddRange(room.sockets);

				await TryLoop();

				ClearLoop();
			}
		}



		private async Task<bool> TryLoop()
		{
			await Task.Delay(500);

			int testingRoomIndex = currentLoop.Count - 1;
			Room lastRoom = currentLoop[testingRoomIndex];

			foreach (Socket socket in lastRoom.sockets)
			{
				if (!socket.isAvailable) continue;

				foreach (RoomData data in roomLibrary)
				{
					if (data.roomPrefab.GetComponent<Room>().sockets.Where(s => s.socketType == socket.socketType).Count() <= 0) continue;

					Room room = Instantiate(data.roomPrefab, Vector3.zero, Quaternion.identity).GetComponent<Room>();

					foreach (Socket incomingSocket in room.sockets)
					{
						if (incomingSocket.socketType != socket.socketType) continue;

						AlignRooms(socket, incomingSocket, room.transform);

						if (IsOverlapping(room, socket)) continue;

						await Task.Delay(500);

						//Check if no good socket found on this room -> the room cannot be placed
						if (incomingSocket == null)
						{
							Debug.Log($"Room {room.roomName} not valid");
							//Remove room from loop and destroy
							DestroyImmediate(room.gameObject);
							continue;
						}

						currentLoop.Add(room);
						socket.isAvailable = false;
						incomingSocket.isAvailable = false;
						//room.transform.SetParent(socket.transform);

						openSocket.AddRange(room.sockets.Where(s => s.isAvailable));

						await Task.Delay(500);

						if (currentLoop.Count >= loopSize)
						{
							foreach (Socket newSocket in room.sockets)
							{
								if (!newSocket.isAvailable) continue;

								foreach (Socket existingSocket in openSocket)
								{
									if (!existingSocket.isAvailable || existingSocket == newSocket) continue;

									if (existingSocket.socketType != newSocket.socketType) continue;

									if (Vector3.Distance(newSocket.socket.transform.position, existingSocket.socket.transform.position) < .1f)
									{
										newSocket.isAvailable = false;
										//existingSocket.isAvailable = false;
										Debug.DrawRay(newSocket.transform.position, Vector3.up * 20, Color.cyan, 5);
										Debug.Log($"Loop found");
										//REGISTER LOOP IN SO
									}
								}
							}

							Debug.Log($"Loop not found");
							Backtrack(room, socket, incomingSocket);
							continue;
						}

						if (await TryLoop()) continue;

						Backtrack(room, socket, incomingSocket);
					}

					Debug.LogWarning("Switch room");

					DestroyImmediate(room.gameObject);

					//Find if room can be placed with one socket
					//Socket incomingSocket = CheckRoomValidityWithSocket(socket, room);
				}
			}
			return false;
		}

		private void Backtrack(Room room, Socket parentSocket, Socket incomingSocket)
		{
			parentSocket.isAvailable = true;
			incomingSocket.isAvailable = true;
			currentLoop.Remove(room);
			foreach (Socket s in room.sockets)
			{
				if (openSocket.Contains(s))
					openSocket.Remove(s);
			}
		}

		private Socket CheckRoomValidityWithSocket(Socket targetSocket, Room room)
		{
			//Find all corresponding sockets in the room socket array
			List<Socket> correspondingSockets = room.sockets.Where(sck => sck.socketType == targetSocket.socketType).ToList();

			//Test all corresponding socket until one fits well with orientation
			while (correspondingSockets.Count > 0)
			{
				//Get a random corresponding socket to test
				Socket incomingSocket = correspondingSockets[0];
				correspondingSockets.RemoveAt(0);

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

			//float padding = .05f;
			//Get the bounds or our room
			//Bounds b = room.boundCollider.bounds;

			//Get all the object colliding with our room (added a small bit of padding for tolerance)
			//Collider[] colliders = Physics.OverlapBox(b.center, (b.extents - Vector3.one * padding), room.transform.rotation, roomLayer);

			Vector2[] incoming = GetWorldFootprint(room);

			//Check on all the room colliding object found
			foreach (Room hitRoom in currentLoop)
			{
				//Room hitRoom = c.transform.GetComponentInParent<Room>();

				//If doesn't have room script, skip
				if (hitRoom == null) continue;

				//If it's the room we're testing for, skip
				if (hitRoom.gameObject == room.gameObject) continue;

				//If it's the room we're buidling from, skip
				if (hitRoom == targetSocket.room) continue;

				//Else, we're colliding with another room, return overlapping to true
				//return true;

				Vector2[] placed = GetWorldFootprint(hitRoom);
				if (PolygonsOverlap(incoming, placed))
				{
					Debug.Log($"{room.roomName} is hitting {hitRoom.roomName} with id {hitRoom.GetInstanceID()}");
					return true;
				}
			}

			//No overlapping found
			return false;
		}

		private bool PolygonsOverlap(Vector2[] polyA, Vector2[] polyB)
		{
			foreach (Vector2[] poly in new[] { polyA, polyB })
			{
				for (int i = 0; i < poly.Length; i++)
				{
					Vector2 edge = poly[(i + 1) % poly.Length] - poly[i];
					Vector2 axis = new Vector2(-edge.y, edge.x);

					Project(polyA, axis, out float minA, out float maxA);
					Project(polyB, axis, out float minB, out float maxB);

					if (maxA <= minB + .05f || maxB <= minA + .05f) return false;
				}
			}
			return true;
		}

		private void Project(Vector2[] poly, Vector2 axis, out float min, out float max)
		{
			min = max = Vector2.Dot(poly[0], axis);
			for (int i = 1; i < poly.Length; i++)
			{
				float p = Vector2.Dot(poly[i], axis);
				if (p < min) min = p;
				if (p > max) max = p;
			}
		}

		private Vector2[] GetWorldFootprint(Room room)
		{
			Vector2[] world = new Vector2[room.footprint.Length];
			for (int i = 0; i < room.footprint.Length; i++)
			{
				Vector3 p = room.transform.TransformPoint(new Vector3(room.footprint[i].x, 0, room.footprint[i].y));
				world[i] = new Vector2(p.x, p.z);
			}
			return world;
		}

		private void ClearLoop()
		{
			//Destroy all room in loop
			for (int i = currentLoop.Count - 1; i >= 0; i--)
			{
				DestroyImmediate(currentLoop[i].gameObject);
			}

			//Clear all Lists
			currentLoop.Clear();
			openSocket.Clear();
		}
	}
}
