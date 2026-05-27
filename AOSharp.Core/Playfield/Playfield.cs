using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.DataTypes;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Common.Unmanaged.DbObjects;
using static AOSharp.Common.Unmanaged.DbObjects.OutdoorRDBTilemap;
using System.IO;
using System.Reflection;

namespace AOSharp.Core
{
    public static unsafe class Playfield
    {
        ///<summary>
        ///Playfield Identity
        ///</summary>
        public static Identity Identity => GetIdentity();

        ///<summary>
        ///Playfield's model identity
        ///</summary>
        public static Identity ModelIdentity => GetModelIdentity();

        public static PlayfieldId ModelId => (PlayfieldId)ModelIdentity.Instance;

        ///<summary>
        ///Are mechs allowed on the playfield
        ///</summary>
        public static bool AllowsVehicles => AreVehiclesAllowed();

        ///<summary>
        ///Is Shadowlands playfield
        ///</summary>
        public static bool IsShadowlands => IsShadowlandPF();

        ///<summary>
        ///Is BattleStation
        ///</summary>
        public static bool IsBattleStation => IsBattleStationPF();

        ///<summary>
        ///Is playfield a dungeon
        ///</summary>
        public static bool IsDungeon => IsDungeonPF();

        ///<summary>
        ///Playfield name
        ///</summary>
        public static string Name => GetName();

        ///<summary>
        ///Get zones (cells) for playfield.
        ///</summary>
        public static List<Zone> Zones => GetZones();

        ///<summary>
        ///Get rooms for playfield if in a dungeon.
        ///</summary>
        public static List<Room> Rooms => GetRooms();

        ///<summary>
        ///Get number of water meshes for playfield.
        ///</summary>
        public static List<Mesh> Water => GetWaters();

        ///<summary>
        ///Get Tower Proxies
        ///</summary>
        public static List<TowerProxy> TowerProxies => GetTowerProxies();

        ///<summary>
        ///Get Tilemap for playfield
        ///</summary>
        public static IntPtr TileMapPtr => GetTilemap();

        ///<summary>
        ///Get RDBTilemap for playfield
        ///</summary>
        public static RDBTilemap RDBTilemap => GetRDBTilemap();

        ///<summary>
        ///Get DB Resource Id for Tilemap
        ///</summary>
        public static int TilemapResourceId => GetTilemapResourceId();

        public static IEnumerable<Door> Doors => DynelManager.Doors;

        public static int NumFloors => IsDungeon ? Rooms.Select(r => r.Floor).Distinct().Count() : 0;

        //TODO: Convert to use n3Playfield_t::GetPlayfieldDynels() to remove dependencies on hard-coded offsets
        internal static unsafe List<IntPtr> GetPlayfieldDynels()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return new List<IntPtr>();

            return (*(Playfield_MemStruct*)pPlayfield).Dynels.ToList();
        }

        private static unsafe Identity GetIdentity()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return Identity.None;

            return *N3Playfield_t.GetIdentity(pPlayfield);
        }

        private static unsafe Identity GetModelIdentity()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return Identity.None;

            return *N3Playfield_t.GetModelID(pPlayfield);
        }

        private static string GetName()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return String.Empty;

            return Marshal.PtrToStringAnsi(N3Playfield_t.GetName(pPlayfield));
        }

        private static unsafe List<Zone> GetZones()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return new List<Zone>();

            return (*N3Playfield_t.GetZones(pPlayfield)).ToList().Select(x => new Zone(x)).ToList();
        }

        private static unsafe List<Room> GetRooms()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero || !IsDungeon)
                return new List<Room>();

            return (*N3Playfield_t.GetZones(pPlayfield)).ToList().Select(x => new Room(x)).ToList();
        }

        private static bool AreVehiclesAllowed()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return false;

            return PlayfieldAnarchy_t.AreVehiclesAllowed(pPlayfield);
        }

        private static bool IsShadowlandPF()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return false;

            return PlayfieldAnarchy_t.IsShadowlandPF(pPlayfield);
        }
        public static bool Raycast(Vector3 pos1, Vector3 pos2, out Vector3 hitPos, out Vector3 hitNormal)
        {
            hitPos = Vector3.Zero;
            hitNormal = Vector3.Zero;

            IntPtr pSurface = GetSurface();

            if (pSurface == IntPtr.Zero)
                return false;

            return IsDungeon ? RoomSurface_t.GetLineIntersection(pSurface, ref pos1, ref pos2, ref hitPos, ref hitNormal, 1, IntPtr.Zero)
                                : TilemapSurface_t.GetLineIntersection(pSurface, ref pos1, ref pos2, ref hitPos, ref hitNormal, 1, IntPtr.Zero);
        }

        public static bool LineOfSight(Vector3 pos1, Vector3 pos2, int zoneCell = 1, bool unknown = false)
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return false;

            return N3Playfield_t.LineOfSight(pPlayfield, &pos1, &pos2, zoneCell, false);
        }

        private static bool IsDungeonPF()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return false;

            return N3Playfield_t.IsDungeon(pPlayfield);
        }

        private static bool IsBattleStationPF()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return false;

            return N3Playfield_t.IsBattleStation(pPlayfield);
        }

        internal static IntPtr GetSurface()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return IntPtr.Zero;

            return N3Playfield_t.GetSurface(pPlayfield);
        }

        internal static IntPtr GetTilemap()
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return IntPtr.Zero;

            return N3Playfield_t.GetTilemap(pPlayfield);
        }

        internal static int GetTilemapResourceId()
        {
            return ((Tilemap_MemStruct*)TileMapPtr)->TilemapResourceId;
        }

        private static List<TowerProxy> GetTowerProxies()
        {
            List<TowerProxy> towerProxies = new List<TowerProxy>();

            IntPtr pLandControlClient = *(IntPtr*)(Kernel32.GetModuleHandle("Gamecode.dll") + Offsets.N3EngineClientAnarchy_t.LandControlClient_t);
            LandControlClient_MemStruct landControlClient = *(LandControlClient_MemStruct*)(pLandControlClient);

            return landControlClient.GetTowerProxies();
        }

        internal static List<Mesh> GetWaters()
        {
            List<Mesh> waterMeshes = new List<Mesh>();

            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();

            if (pPlayfield == IntPtr.Zero)
                return waterMeshes;

            int numWaters = PlayfieldAnarchy_t.GetNumberOfWaters(pPlayfield);

            if (numWaters == 0)
                return waterMeshes;

            IntPtr pWaterData = PlayfieldAnarchy_t.GetWaters(pPlayfield);

            using (UnmanagedMemoryStream unmanagedMemStream = new UnmanagedMemoryStream((byte*)pWaterData.ToPointer(), 0x28 * numWaters))
            {
                using (BinaryReader reader = new BinaryReader(unmanagedMemStream))
                {
                    for (int i = 0; i < numWaters; i++)
                    {
                        waterMeshes.Add(new Mesh
                        {
                            Vertices = new List<Vector3>()
                            {
                                new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),
                                new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),
                                new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),
                            },
                            Triangles = new List<int> { 2, 1, 0 }
                        });

                        //Unk - Always 0
                        reader.ReadInt32();
                    }
                }
            }

            return waterMeshes;
        }

        internal static RDBTilemap GetRDBTilemap()
        {
            IntPtr rdbTilemapPtr = ((Tilemap_MemStruct*)TileMapPtr)->RDBTilemapPtr;
            return IsDungeon ? DungeonRDBTilemap.FromPointer(rdbTilemapPtr) : (RDBTilemap)OutdoorRDBTilemap.FromPointer(rdbTilemapPtr);
        }

        internal static bool IsDoorOpenBetweenRooms(short roomInst1, short roomInst2)
        {
            IntPtr pPlayfield = N3EngineClient_t.GetPlayfield();
            return pPlayfield != IntPtr.Zero && N3Playfield_t.IsDoorOpenBetweenRooms(pPlayfield, roomInst1, roomInst2);
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        public struct TowerProxy
        {
            [FieldOffset(0x0)]
            public Identity ProxyIdentity;

            [FieldOffset(0x8)]
            public Identity Identity;

            [FieldOffset(0x10)]
            public Vector3 Position;

            [FieldOffset(0x20)]
            public Side Side;

            [FieldOffset(0x2C)]
            public TowerClass Class;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        private struct Playfield_MemStruct
        {
            [FieldOffset(0x30)]
            public StdObjVector Dynels;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        private struct LandControlClient_MemStruct
        {
            [FieldOffset(0x14)]
            public IntPtr pTowerProxies;

            [FieldOffset(0x18)]
            public int Towercount;

            public unsafe List<TowerProxy> GetTowerProxies()
            {
                List<TowerProxy> towerProxies = new List<TowerProxy>();

                IntPtr pCurrent = *(IntPtr*)pTowerProxies;

                for(int i =  0; i < Towercount; i++)
                {
                    towerProxies.Add(*(TowerProxy*)(pCurrent + 0xC));

                    pCurrent = *(IntPtr*)(pCurrent);
                }

                return towerProxies;
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        private struct Tilemap_MemStruct
        {
            [FieldOffset(0x08)]
            public int TilemapResourceId;

            [FieldOffset(0x0C)]
            public IntPtr RDBTilemapPtr;
        }
    }
}
