using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace AOSharp.Core
{
    public static class Extensions
    {
        public static T Cast<T>(this Dynel dynel) where T : Dynel
        {
            return (T)Activator.CreateInstance(typeof(T), dynel.Pointer);
        }

        public static bool Find(this Buff[] buffs, Spell spell, out Buff buff)
        {
            return (buff = buffs.FirstOrDefault(x => x.Id == spell.Id)) != null;
        }

        public static bool Find(this Buff[] buffs, int id, out Buff buff)
        {
            return (buff = buffs.FirstOrDefault(x => x.Id == id)) != null;
        }

        public static bool Find(this Buff[] buffs, NanoLine nanoline, out Buff buff)
        {
            return (buff = buffs.FirstOrDefault(x => (NanoLine)x.GetStat(Stat.NanoStrain) == nanoline)) != null;
        }

        public static bool Find(this Buff[] buffs, int[] ids, out Buff buff)
        {
            return (buff = buffs.FirstOrDefault(b => ids.Contains(b.Id))) != null;
        }

        public static bool Contains(this Buff[] buffs, int id)
        {
            return Contains(buffs, new[] { id });
        }

        public static bool Contains(this Buff[] buffs, int[] ids)
        {
            return buffs.Any(b => ids.Contains(b.Id));
        }

        public static bool Contains(this Buff[] buffs, NanoLine nanoline)
        {
            return buffs.Any(b => (NanoLine) b.GetStat(Stat.NanoStrain) == nanoline);
        }

        public static bool Contains(this Buff[] buffs, NanoLine[] nanolines)
        {
            return buffs.Any(b => nanolines.Contains((NanoLine)b.GetStat(Stat.NanoStrain)));
        }

        public static IEnumerable<Spell> OrderByStackingOrder(this IEnumerable<Spell> spells)
        {
            return spells.OrderByDescending(x => x.StackingOrder);
        }

        public static int[] GetIds(this IEnumerable<Spell> spells)
        {
            return spells.Select(x => x.Id).ToArray();
        }

        public static bool Contains(this List<TeamMember> teamMembers, Identity identity)
        {
            return teamMembers.Select(x => x.Identity).Contains(identity);
        }

        public static bool Contains(this Pet[] pets, Identity identity)
        {
            return pets.Select(x => x.Identity).Contains(identity);
        }

        public static IEnumerable<SpellData> FindAll(this List<SpellData> spellDataList, SpellFunction func)
        {
            return spellDataList.Where(b => b.Function == func);
        }

        public static void Draw(this Mesh mesh, float maxDrawDist)
        {
            for (int j = 0; j < mesh.Triangles.Count / 3; j++)
            {
                int tri = j * 3;
                int tri1 = mesh.Triangles[tri];
                int tri2 = mesh.Triangles[tri + 1];
                int tri3 = mesh.Triangles[tri + 2];

                Vector3[] verts = new Vector3[3]
                {
                    mesh.LocalToWorldMatrix.MultiplyPoint3x4(mesh.Vertices[tri1]),
                    mesh.LocalToWorldMatrix.MultiplyPoint3x4(mesh.Vertices[tri2]),
                    mesh.LocalToWorldMatrix.MultiplyPoint3x4(mesh.Vertices[tri3])
                };

                if (verts.Any(x => Vector3.Distance(x, DynelManager.LocalPlayer.Position) > maxDrawDist))
                    continue;

                Debug.DrawLine(verts[0], verts[1], DebuggingColor.Green);
                Debug.DrawLine(verts[1], verts[2], DebuggingColor.Green);
                Debug.DrawLine(verts[2], verts[0], DebuggingColor.Green);
            }
        }

        public static void DrawVertices(this Mesh mesh, float maxDrawDist)
        {
            foreach(Vector3 vert in mesh.Vertices)
                Debug.DrawSphere(mesh.LocalToWorldMatrix.MultiplyPoint3x4(vert), 0.1f, DebuggingColor.Red);
        }

        public static string GetLineName(this ResearchGoal researchGoal)
        {
            return N3EngineClientAnarchy.GetPerkName(researchGoal.ResearchId);
        }
    }
}
