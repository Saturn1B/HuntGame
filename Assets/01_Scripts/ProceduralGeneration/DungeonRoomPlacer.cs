using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HuntingGame.ProceduralGeneration
{
    /// <summary>
    /// Single responsibility: decide what to place next on a given open socket (a pre-baked loop,
    /// an active bridge back into the existing dungeon, a normal room, or a dead-end room) and
    /// instantiate it. Extracted out of DungeonGenerator, which now only orchestrates the overall
    /// generation loop (safety break, regenerate-if-too-small, dead-end closing).
    /// </summary>
    public sealed class DungeonRoomPlacer
    {
        private readonly RoomData[] _roomLibrary;
        private readonly LoopData[] _loopLibrary;
        private readonly DungeonGhostPool _ghostPool;
        private readonly Transform _parent;
        private readonly int _deepness;
        private readonly float _loopProbability;
        private readonly bool _tryLooping;

        public DungeonRoomPlacer(
            RoomData[] roomLibrary,
            LoopData[] loopLibrary,
            DungeonGhostPool ghostPool,
            Transform parent,
            int deepness,
            float loopProbability,
            bool tryLooping)
        {
            _roomLibrary = roomLibrary;
            _loopLibrary = loopLibrary;
            _ghostPool = ghostPool;
            _parent = parent;
            _deepness = deepness;
            _loopProbability = loopProbability;
            _tryLooping = tryLooping;
        }

        public bool TryPlaceRoom(Socket targetSocket, List<Room> spawnedRoom, List<Socket> openSocket)
        {
            _ghostPool.EnsureSetup(_roomLibrary);

            //Random try placing a loop from loop library
            if (Random.value < _loopProbability)
            {
                if (TryPlaceLoop(targetSocket, spawnedRoom, openSocket)) return true;
            }

            //REDUNDANT, MIGHT REMOVE THE ACTIVE LOOP GENERATION
            //Check if we want to actively try looping
            if (_tryLooping)
            {
                //Before generating a room, we check to see if we can't bridge two existing socket
                //Check on all open socket
                foreach (Socket otherSocket in openSocket.ToList())
                {
                    //if the socket is not available or it's from the same room as the other socket we're trying to bridge with, we skip
                    if (!otherSocket.isAvailable || otherSocket.room == targetSocket.room) continue;

                    //get the distance between the two socket
                    float dist = Vector3.Distance(targetSocket.transform.position, otherSocket.transform.position);

                    //if the two socket are in acceptable range, try bridging between them
                    if (dist > 1f && dist < 20)
                        if (TryBridgeRoom(targetSocket, otherSocket, spawnedRoom, openSocket)) return true;
                }
            }

            //Find all room having corresponding socket
            List<RoomData> correspondingRooms = _roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)).ToList();

            //If room count is bellow a certain number, remove all dead end from coresponding rooms
            if (spawnedRoom.Count < _deepness * .75f)
                correspondingRooms = correspondingRooms.Where(r => r.roomPrefab.GetComponent<Room>().sockets.Length > 1).ToList();

            //Test all corresponding room until one fits well with orientation
            while (correspondingRooms.Count > 0)
            {
                //Get a random corresponding room to test by weight
                RoomData selectedData = GetWeightedRandomRoom(correspondingRooms);
                correspondingRooms.Remove(selectedData);

                Room ghostRoom = _ghostPool.GetGhost(selectedData);

                //Find if room can be placed with one socket
                Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom, spawnedRoom);

                //Check if no good socket found on this room -> the room cannot be placed
                if (incomingSocket == null)
                {
                    //Return ghost room to pool
                    ghostRoom.gameObject.SetActive(false);
                    continue;
                }

                //If good socket found -> the room can be placed, stop search there
                GameObject realRoomObj = Object.Instantiate(selectedData.roomPrefab, _parent);
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

                DungeonGeometryUtils.CheckForNaturalLoop(realRoom, openSocket);

                return true;
            }

            //return the state of our search, did we found a room to place or not
            return false;
        }

        public bool TryPlaceEndRoom(Socket targetSocket, List<Room> spawnedRoom, List<Socket> openSocket)
        {
            _ghostPool.EnsureSetup(_roomLibrary);

            //Find all room having corresponding socket that are dead ends
            List<RoomData> correspondingRooms = _roomLibrary.Where(obj => obj.socketTypes.Contains(targetSocket.socketType)
                                                    && obj.roomPrefab.GetComponent<Room>().sockets.Length == 1).ToList();

            //Test all corresponding room until one fits well with orientation
            while (correspondingRooms.Count > 0)
            {
                //Get a random corresponding room to test by weight
                RoomData selectedData = GetWeightedRandomRoom(correspondingRooms);
                correspondingRooms.Remove(selectedData);

                Room ghostRoom = _ghostPool.GetGhost(selectedData);

                //Find if room can be placed with one socket
                Socket incomingSocket = CheckRoomValidityWithSocket(targetSocket, ghostRoom, spawnedRoom);

                //Check if no good socket found on this room -> the room cannot be placed
                if (incomingSocket == null)
                {
                    //Return ghost room to pool
                    ghostRoom.gameObject.SetActive(false);
                    continue;
                }

                //If good socket found -> the room can be placed, stop search there
                GameObject realRoomObj = Object.Instantiate(selectedData.roomPrefab, _parent);
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

        private bool TryPlaceLoop(Socket targetSocket, List<Room> spawnedRoom, List<Socket> openSocket)
        {
            if (_loopLibrary == null || _loopLibrary.Length == 0) return false;

            //Shuffle library to avaoid always picking the same room
            List<LoopData> loopsToTry = _loopLibrary.OrderBy(x => Random.value).ToList();

            foreach (LoopData loop in loopsToTry)
            {
                for (int i = 0; i < loop.roomLoop.Count; i++)
                {
                    RoomData anchorData = loop.roomLoop[i];
                    Room anchorGhost = _ghostPool.GetGhost(anchorData);

                    //Check every socket on this specific room in the loop
                    foreach (Socket incomingSocket in anchorGhost.sockets)
                    {
                        if (incomingSocket.socketType != targetSocket.socketType) continue;

                        //Calculate world pos for the anchor room based on target socket
                        DungeonGeometryUtils.AlignRooms(targetSocket, incomingSocket, anchorGhost.transform);

                        Vector3 worldAnchorPos = anchorGhost.transform.position;
                        Quaternion worldAnchorRot = anchorGhost.transform.rotation;
                        Vector3 localAnchorPos = loop.relativePoseLoop[i].position;
                        Quaternion localAnchorRot = loop.relativePoseLoop[i].rotation;

                        //Pre calculate world pose for entire loop and check overlaps
                        List<Pose> worldPoses = new List<Pose>();
                        bool anyOverlap = false;

                        for (int j = 0; j < loop.roomLoop.Count; j++)
                        {
                            //Calculate relative transform from anchor to current room in loop space
                            Quaternion relRot = Quaternion.Inverse(localAnchorRot) * loop.relativePoseLoop[j].rotation;
                            Vector3 relPos = Quaternion.Inverse(localAnchorRot) * (loop.relativePoseLoop[j].position - localAnchorPos);

                            //Map loop space coord to world space based on our anchor alignment
                            Vector3 worldPos = worldAnchorPos + (worldAnchorRot * relPos);
                            Quaternion worldRot = worldAnchorRot * relRot;
                            worldPoses.Add(new Pose(worldPos, worldRot));

                            //Use ghot to verify room doesn't hit the existing dungeon
                            Room checkGhost = _ghostPool.GetGhost(loop.roomLoop[j]);
                            checkGhost.transform.position = worldPos;
                            checkGhost.transform.rotation = worldRot;

                            Room ignoreTarget = (j == i) ? targetSocket.room : null;

                            if (DungeonGeometryUtils.IsOverlappingAny(checkGhost, spawnedRoom, ignoreTarget))
                            {
                                anyOverlap = true;
                                checkGhost.gameObject.SetActive(false);
                                break;
                            }
                            checkGhost.gameObject.SetActive(false);
                        }

                        //Check if any overlap, skip to try a different anchor or loop
                        if (anyOverlap) continue;

                        //Entire loop fits ! Instantiate all rooms
                        for (int j = 0; j < loop.roomLoop.Count; j++)
                        {
                            GameObject realRoomObj = Object.Instantiate(loop.roomLoop[j].roomPrefab, _parent);
                            Room realRoom = realRoomObj.GetComponent<Room>();
                            realRoom.transform.position = worldPoses[j].position;
                            realRoom.transform.rotation = worldPoses[j].rotation;

                            //Connect room anchor to dungeon entry socket
                            if (j == i)
                            {
                                targetSocket.isAvailable = false;
                                int socketIndex = System.Array.IndexOf(anchorGhost.sockets, incomingSocket);
                                realRoom.sockets[socketIndex].isAvailable = false;
                            }

                            spawnedRoom.Add(realRoom);
                            openSocket.AddRange(realRoom.sockets.Where(s => s.isAvailable));

                            DungeonGeometryUtils.CheckForNaturalLoop(realRoom, openSocket);
                        }

                        //Loop succesfully placed
                        anchorGhost.gameObject.SetActive(false);
                        Debug.Log("Loop succesfully generated");
                        return true;
                    }
                    anchorGhost.gameObject.SetActive(false);
                }
            }
            return false;
        }

        private bool TryBridgeRoom(Socket socketA, Socket socketB, List<Room> spawnedRoom, List<Socket> openSocket)
        {
            _ghostPool.EnsureSetup(_roomLibrary);

            //Find all room having having at least 2 socket
            List<RoomData> candidates = _roomLibrary.Where(r => r.roomPrefab.GetComponent<Room>().sockets.Length >= 2).ToList();

            //Test all candidates
            foreach (var data in candidates)
            {
                //Spawn the room to test
                Room ghostRoom = _ghostPool.GetGhost(data);
                var ghostSockets = ghostRoom.sockets;

                for (int i = 0; i < ghostSockets.Length; i++)
                {
                    //if the first socket of the room is not of the same type as the socket we're trying to connect, we skip
                    if (ghostSockets[i].socketType != socketA.socketType) continue;

                    //Align room to target door
                    DungeonGeometryUtils.AlignRooms(socketA, ghostSockets[i], ghostRoom.transform);

                    for (int j = 0; j < ghostSockets.Length; j++)
                    {
                        //if ghost room socket is the same as the one we already tested, or is not of the same type as the socket we're trying to connect, we skip
                        if (i == j) continue;
                        if (ghostSockets[j].socketType != socketB.socketType) continue;

                        float dist = Vector3.Distance(ghostSockets[j].transform.position, socketB.transform.position);
                        float angle = Quaternion.Angle(ghostSockets[j].transform.rotation, Quaternion.LookRotation(-socketB.transform.forward, socketB.transform.up));

                        //Check if the two socket are superposed
                        if (dist < .1f && angle < 1f)
                        {
                            //Check for room overlaping
                            if (!DungeonGeometryUtils.IsOverlappingAny(ghostRoom, spawnedRoom, socketA.room))
                            {
                                //Bridge is valid, validate the room and socket

                                GameObject realRoomObj = Object.Instantiate(data.roomPrefab, _parent);
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

        private Socket CheckRoomValidityWithSocket(Socket targetSocket, Room room, List<Room> spawnedRoom)
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
                DungeonGeometryUtils.AlignRooms(targetSocket, incomingSocket, room.transform);

                //Check for room overlaping
                if (DungeonGeometryUtils.IsOverlappingAny(room, spawnedRoom, targetSocket.room)) continue;

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
    }
}
