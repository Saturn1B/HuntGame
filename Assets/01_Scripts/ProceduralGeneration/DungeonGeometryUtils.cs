using System.Collections.Generic;
using UnityEngine;

namespace HuntingGame.ProceduralGeneration
{
    /// <summary>
    /// Shared 2D footprint / socket-alignment math used by both DungeonGenerator (runtime
    /// generation) and LoopPregenerator (editor-time loop baking). Previously duplicated
    /// near-verbatim in both classes.
    /// </summary>
    public static class DungeonGeometryUtils
    {
        public static void AlignRooms(Socket anchor, Socket incoming, Transform roomTransform)
        {
            // Calculate target rotation, making it face opposite direction of anchor, while keeping the room up
            Quaternion targetSocketRot = Quaternion.LookRotation(-anchor.transform.forward, anchor.transform.up);

            // Calculate rotation offset to know how much our room should rotate reach target socket rotation
            Quaternion rotationOffset = targetSocketRot * Quaternion.Inverse(incoming.transform.localRotation);
            roomTransform.rotation = rotationOffset;

            // Calculate position offset an place the room with that offset
            Vector3 positionOffset = anchor.socket.transform.position - incoming.socket.transform.position;
            roomTransform.position += positionOffset;
        }

        /// <summary>
        /// Checks whether <paramref name="room"/> overlaps any room in <paramref name="existingRooms"/>,
        /// ignoring the room itself and <paramref name="roomToIgnore"/> (the room it's being built from).
        /// </summary>
        public static bool IsOverlappingAny(Room room, IEnumerable<Room> existingRooms, Room roomToIgnore)
        {
            Vector2[] incoming = GetWorldFootprint(room);

            foreach (Room hitRoom in existingRooms)
            {
                if (hitRoom == null) continue;
                if (hitRoom.gameObject == room.gameObject) continue;
                if (hitRoom == roomToIgnore) continue;

                Vector2[] placed = GetWorldFootprint(hitRoom);
                if (PolygonsOverlap(incoming, placed)) return true;
            }

            return false;
        }

        public static bool PolygonsOverlap(Vector2[] polyA, Vector2[] polyB)
        {
            foreach (Vector2[] poly in new[] { polyA, polyB })
            {
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 edge = poly[(i + 1) % poly.Length] - poly[i];
                    Vector2 axis = new Vector2(-edge.y, edge.x);

                    float magnitude = axis.magnitude;
                    if (magnitude < 0.0001f) continue; // ignore les aretes degenerees
                    axis /= magnitude;

                    Project(polyA, axis, out float minA, out float maxA);
                    Project(polyB, axis, out float minB, out float maxB);

                    if (maxA <= minB + .05f || maxB <= minA + .05f) return false;
                }
            }
            return true;
        }

        public static void Project(Vector2[] poly, Vector2 axis, out float min, out float max)
        {
            min = max = Vector2.Dot(poly[0], axis);
            for (int i = 1; i < poly.Length; i++)
            {
                float p = Vector2.Dot(poly[i], axis);
                if (p < min) min = p;
                if (p > max) max = p;
            }
        }

        public static Vector2[] GetWorldFootprint(Room room)
        {
            Vector2[] world = new Vector2[room.footprint.Length];
            for (int i = 0; i < room.footprint.Length; i++)
            {
                Vector3 p = room.transform.TransformPoint(new Vector3(room.footprint[i].x, 0, room.footprint[i].y));
                world[i] = new Vector2(p.x, p.z);
            }
            return world;
        }
    }
}
