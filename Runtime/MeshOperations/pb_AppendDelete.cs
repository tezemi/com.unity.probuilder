using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;

namespace ProBuilder.MeshOperations
{
	/// <summary>
	/// Functions for appending or deleting faces from pb_Object meshes.
	/// </summary>
	public static class pb_AppendDelete
	{
		/// <summary>
		/// Append a new face to the pb_Object using sharedIndex array to set the face indices to sharedIndex groups.
		/// </summary>
		/// <param name="pb">The pb_Object mesh target.</param>
		/// <param name="positions">The new vertex positions to add.</param>
		/// <param name="colors">The new colors to add (must match positions length).</param>
		/// <param name="uvs">The new uvs to add (must match positions length).</param>
		/// <param name="face">A </param>
		/// <returns></returns>
		public static Face AppendFace(this ProBuilderMesh pb, Vector3[] positions, Color[] colors, Vector2[] uvs, Face face)
		{
			int[] shared = new int[positions.Length];
			for(int i = 0; i < positions.Length; i++)
				shared[i] = -1;
			return pb.AppendFace(positions, colors, uvs, face, shared);
		}

		/// <summary>
		/// Append a new face to the pb_Object using sharedIndex array to set the face indices to sharedIndex groups.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="v"></param>
		/// <param name="c"></param>
		/// <param name="u"></param>
		/// <param name="face"></param>
		/// <param name="sharedIndex"></param>
		/// <returns></returns>
		public static Face AppendFace(this ProBuilderMesh pb, Vector3[] v, Color[] c, Vector2[] u, Face face, int[] sharedIndex)
		{
			int vertexCount = pb.vertexCount;

			Vector3[] _verts = new Vector3[vertexCount + v.Length];
			Color[] _colors = new Color[vertexCount + c.Length];
			Vector2[] _uvs = new Vector2[pb.uv.Length + u.Length];

			List<Face> _faces = new List<Face>(pb.faces);
			IntArray[] sharedIndices = pb.sharedIndices;

			// copy new vertices
			System.Array.Copy(pb.positions, 0, _verts, 0, vertexCount);
			System.Array.Copy(v, 0, _verts, vertexCount, v.Length);

			// copy new colors
			System.Array.Copy(pb.colors, 0, _colors, 0, vertexCount);
			System.Array.Copy(c, 0, _colors, vertexCount, c.Length);

			// copy new uvs
			System.Array.Copy(pb.uv, 0, _uvs, 0, pb.uv.Length);
			System.Array.Copy(u, 0, _uvs, pb.uv.Length, u.Length);

			face.ShiftIndicesToZero();
			face.ShiftIndices(vertexCount);
			face.RebuildCaches();

			_faces.Add(face);

			for(int i = 0; i < sharedIndex.Length; i++)
				IntArrayUtility.AddValueAtIndex(ref sharedIndices, sharedIndex[i], i+vertexCount);

			pb.SetVertices( _verts );
			pb.SetColors( _colors );
			pb.SetUV( _uvs );

			pb.SetSharedIndices(sharedIndices);
			pb.SetFaces(_faces.ToArray());

			return face;
		}

		/// <summary>
		/// Append a group of new faces to the pb_Object. Significantly faster than calling AppendFace multiple times.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="new_Vertices"></param>
		/// <param name="new_Colors"></param>
		/// <param name="new_uvs"></param>
		/// <param name="new_Faces"></param>
		/// <param name="new_SharedIndices"></param>
		/// <returns></returns>
		public static Face[] AppendFaces(this ProBuilderMesh pb, Vector3[][] new_Vertices, Color[][] new_Colors, Vector2[][] new_uvs, Face[] new_Faces, int[][] new_SharedIndices)
		{
			List<Vector3> _verts = new List<Vector3>(pb.positions);
			List<Color> _colors = new List<Color>(pb.colors);
			List<Vector2> _uv = new List<Vector2>(pb.uv);

			List<Face> _faces = new List<Face>(pb.faces);
			IntArray[] sharedIndices = pb.sharedIndices;

			int vc = pb.vertexCount;

			for(int i = 0; i < new_Faces.Length; i++)
			{
				_verts.AddRange(new_Vertices[i]);
				_colors.AddRange(new_Colors[i]);
				_uv.AddRange(new_uvs[i]);

				new_Faces[i].ShiftIndicesToZero();
				new_Faces[i].ShiftIndices(vc);
				new_Faces[i].RebuildCaches();
				_faces.Add(new_Faces[i]);

				if(new_SharedIndices != null && new_Vertices[i].Length != new_SharedIndices[i].Length)
				{
					Debug.LogError("Append Face failed because sharedIndex array does not match new vertex array.");
					return null;
				}

				if(new_SharedIndices != null)
				{
					for(int j = 0; j < new_SharedIndices[i].Length; j++)
					{
						IntArrayUtility.AddValueAtIndex(ref sharedIndices, new_SharedIndices[i][j], j+vc);
					}
				}
				else
				{
					for(int j = 0; j < new_Vertices[i].Length; j++)
					{
						IntArrayUtility.AddValueAtIndex(ref sharedIndices, -1, j+vc);
					}
				}

				vc = _verts.Count;
			}

			pb.SetSharedIndices(sharedIndices);

			pb.SetVertices(_verts.ToArray());
			pb.SetColors(_colors.ToArray());
			pb.SetUV(_uv.ToArray());
			pb.SetFaces(_faces.ToArray());

			return new_Faces;
		}

		/// <summary>
		/// Duplicate and reverse the winding direction for each face.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="faces"></param>
		public static void DuplicateAndFlip(this ProBuilderMesh pb, Face[] faces)
		{
			List<FaceRebuildData> rebuild = new List<FaceRebuildData>();
			List<Vertex> vertices = new List<Vertex>(Vertex.GetVertices(pb));
			Dictionary<int, int> lookup = pb.sharedIndices.ToDictionary();

			foreach(Face face in faces)
			{
				FaceRebuildData data = new FaceRebuildData();

				data.vertices = new List<Vertex>();
				data.face = new Face(face);
				data.sharedIndices = new List<int>();

				Dictionary<int, int> map = new Dictionary<int, int>();
				int len = data.face.indices.Length;

				for(int i = 0; i < len; i++)
				{
					if(map.ContainsKey(face.indices[i]))
						continue;

					map.Add(face.indices[i], map.Count);
					data.vertices.Add(vertices[face.indices[i]]);
					data.sharedIndices.Add(lookup[face.indices[i]]);
				}

				for(int i = 0; i < len; i++)
					data.face.indices[i] = map[data.face.indices[i]];

				data.face.ReverseIndices();
				rebuild.Add(data);
			}

			FaceRebuildData.Apply(rebuild, pb, vertices, null, lookup, null);
		}

		/// <summary>
		/// Removes the passed face from this pb_Object.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="face"></param>
		/// <returns></returns>
		public static int[] DeleteFace(this ProBuilderMesh pb, Face face)
		{
			return DeleteFaces(pb, new Face[] { face });
		}

		/// <summary>
		/// Remove a set of faces from a pb_Object.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="faces"></param>
		/// <returns></returns>
		public static int[] DeleteFaces(this ProBuilderMesh pb, IEnumerable<Face> faces)
		{
			return DeleteFaces(pb, faces.Select(x => System.Array.IndexOf(pb.faces, x)).ToList());
		}

		/// <summary>
		/// Remove faces from an object by their index in the pb_Object.faces array.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="faceIndices"></param>
		/// <returns></returns>
		public static int[] DeleteFaces(this ProBuilderMesh pb, IList<int> faceIndices)
		{
			Face[] faces = new Face[faceIndices.Count];

			for(int i = 0; i < faces.Length; i++)
				faces[i] = pb.faces[faceIndices[i]];

			List<int> indicesToRemove = faces.SelectMany(x => x.distinctIndices).Distinct().ToList(); // pb_Face.AllTrianglesDistinct(faces);
			indicesToRemove.Sort();

			int vertexCount = pb.positions.Length;

			Vector3[] verts 	= pb.positions.SortedRemoveAt(indicesToRemove);
			Color[] cols 		= pb.colors.SortedRemoveAt(indicesToRemove);
			Vector2[] uvs 		= pb.uv.SortedRemoveAt(indicesToRemove);
			Face[] nFaces 	= pb.faces.RemoveAt(faceIndices);


			Dictionary<int, int> shiftmap = new Dictionary<int, int>();

			for(int i = 0;  i < vertexCount; i++)
				shiftmap.Add(i, InternalUtility.NearestIndexPriorToValue<int>(indicesToRemove, i) + 1);

			// shift all other face indices down to account for moved vertex positions
			for(int i = 0; i < nFaces.Length; i++)
			{
				int[] tris = nFaces[i].indices;

				for(int n = 0; n < tris.Length; n++)
					tris[n] -= shiftmap[tris[n]];

				nFaces[i].SetIndices(tris);
			}


			// shift all other face indices in the shared index array down to account for moved vertex positions
			IntArray[] si = pb.sharedIndices;
			IntArray[] si_uv = pb.sharedIndicesUV;

			IntArrayUtility.RemoveValuesAndShift(ref si, indicesToRemove);
			IntArrayUtility.RemoveValuesAndShift(ref si_uv, indicesToRemove);

			pb.SetSharedIndices(si);
			pb.SetSharedIndicesUV(si_uv);

			pb.SetVertices(verts);
			pb.SetColors(cols);
			pb.SetUV(uvs);

			pb.SetFaces(nFaces);

			int[] array = indicesToRemove.ToArray();

			return array;
		}
	}
}
