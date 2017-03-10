using UnityEngine;
using UnityEditor;
using ProBuilder2.Common;
using System.Collections.Generic;
using ProBuilder2.Interface;

namespace ProBuilder2.EditorCommon
{
	[CustomEditor(typeof(pb_PolyShape))]
	public class pb_PolyShapeTool : Editor
	{
		private static Color HANDLE_COLOR = new Color(.8f, .8f, .8f, 1f);
		private static Color HANDLE_GREEN = new Color(.01f, .9f, .3f, 1f);
		private static Color SELECTED_COLOR = new Color(.01f, .8f, .98f, 1f);

		[SerializeField] private Material m_LineMaterial;
		private Mesh m_LineMesh = null;
		private Plane m_Plane = new Plane(Vector3.up, Vector3.zero);
		private bool m_PlacingPoint = false;
		[SerializeField] private int m_SelectedIndex = -2;
		// private HashSet<int> m_SelectedIndices = new HashSet<int>();

		private pb_PolyShape polygon { get { return target as pb_PolyShape; } }

		void OnEnable()
		{
			pb_Editor.AddOnEditLevelChangedListener(OnEditLevelChange);
			m_LineMesh = new Mesh();
			Undo.undoRedoPerformed += UndoRedoPerformed;
			DrawPolyLine(polygon.points);
			EditorApplication.update += Update;
		}

		void OnDisable()
		{
			pb_Editor.RemoveOnEditLevelChangedListener(OnEditLevelChange);
			GameObject.DestroyImmediate(m_LineMesh);
			EditorApplication.update -= Update;
			Undo.undoRedoPerformed -= UndoRedoPerformed;
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();

			// polygon.polyEditMode = (pb_PolyShape.PolyEditMode) EditorGUILayout.EnumPopup("Edit Mode", polygon.polyEditMode);

			switch(polygon.polyEditMode)
			{
				case pb_PolyShape.PolyEditMode.None:
				{
					if( GUILayout.Button("Edit Poly Shape") )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);
					break;
				}

				case pb_PolyShape.PolyEditMode.Path:
				{
					EditorGUILayout.HelpBox("\nClick To Add Points\n\nPress 'Enter' or 'Space' to Set Height\n", MessageType.Info);
					break;
				}

				case pb_PolyShape.PolyEditMode.Height:
				{
					EditorGUILayout.HelpBox("\nMove Mouse to Set Height\n\nPress 'Enter' or 'Space' to Finalize\n", MessageType.Info);
					break;
				}

				case pb_PolyShape.PolyEditMode.Edit:
				{
					if( GUILayout.Button("Editing Poly Shape", pb_GUI_Utility.GetActiveStyle("Button")) )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.None);
					break;
				}

			}
			
			if(EditorGUI.EndChangeCheck())
			{
				if(pb_Editor.instance != null)
					pb_Editor.instance.SetEditLevel(EditLevel.Plugin);

				if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.None)
					Tools.current = Tool.None;
			}

			GUILayout.Label("selected : " + m_SelectedIndex);
		}

		void Update()
		{
			m_LineMaterial.SetFloat("_EditorTime", (float) EditorApplication.timeSinceStartup);
		}

		void SetPolyEditMode(pb_PolyShape.PolyEditMode mode)
		{
			if(mode != polygon.polyEditMode)
			{
				polygon.polyEditMode = mode;
				UpdateMesh();
			}
			else
			{
				polygon.polyEditMode = mode;
			}
		}

		/**
		 *	Update the pb_Object with the new coordinates.  Returns true if mesh successfully triangulated, false if not.
		 */
		bool UpdateMesh(bool vertexCountChanged = true)
		{
			DrawPolyLine(polygon.points);

			if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path || !polygon.Refresh())
			{
				polygon.mesh.SetVertices(new Vector3[0]);
				polygon.mesh.SetFaces(new pb_Face[0]);
				polygon.mesh.SetSharedIndices(new pb_IntArray[0]);
				polygon.mesh.ToMesh();
				polygon.mesh.Refresh();
				pb_Editor.Refresh();

				return false;
			}

			if(pb_Editor.instance != null)
			{
				if(!vertexCountChanged)
					pb_Editor.instance.Internal_UpdateSelectionFast();
				else
					pb_Editor.Refresh();
			}

			return true;
		}

		void OnSceneGUI()
		{
			if(polygon == null || (polygon.polyEditMode == pb_PolyShape.PolyEditMode.None) || Tools.current != Tool.None)
			{
				if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.None)
				{
					polygon.polyEditMode = pb_PolyShape.PolyEditMode.None;
				}

				return;
			}

			m_LineMaterial.SetPass(0);

			Graphics.DrawMeshNow(m_LineMesh, polygon.transform.localToWorldMatrix, 0);

			Event evt = Event.current;

			DoExistingPointsGUI();

			if(evt.type == EventType.KeyDown)
				HandleKeyEvent(evt.keyCode, evt.modifiers);

			if( pb_Handle_Utility.SceneViewInUse(evt) )
				return;

			int controlID = GUIUtility.GetControlID(FocusType.Passive);

			HandleUtility.AddDefaultControl(controlID);

			DoPointPlacement( HandleUtility.GUIPointToWorldRay(evt.mousePosition) );
		}

		void DoPointPlacement(Ray ray)
		{
			EventType eventType = Event.current.type;

			if(m_PlacingPoint)
			{
				if(	eventType == EventType.MouseDrag )
				{
					float hitDistance = Mathf.Infinity;

					m_Plane.SetNormalAndPosition(polygon.transform.up, polygon.transform.position);

					if( m_Plane.Raycast(ray, out hitDistance) )
					{
						polygon.points[m_SelectedIndex] = polygon.transform.InverseTransformPoint(ray.GetPoint(hitDistance));
						UpdateMesh();
						SceneView.RepaintAll();
					}
				}

				if( eventType == EventType.MouseUp ||
					eventType == EventType.Ignore ||
					eventType == EventType.KeyDown ||
					eventType == EventType.KeyUp )
				{
					m_PlacingPoint = false;
					m_SelectedIndex = -1;
					SceneView.RepaintAll();
				}
			}
			else if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path)
			{
				if( eventType == EventType.MouseDown )
				{
					float hitDistance = Mathf.Infinity;
					m_Plane.SetNormalAndPosition(polygon.transform.up, polygon.transform.position);

					if( m_Plane.Raycast(ray, out hitDistance) )
					{
						pbUndo.RecordObject(polygon, "Add Polygon Shape Point");
						polygon.points.Add(polygon.transform.InverseTransformPoint(ray.GetPoint(hitDistance)));
						m_PlacingPoint = true;
						m_SelectedIndex = polygon.points.Count - 1;
						UpdateMesh();
					}
				}
			}
			else if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Edit)
			{
				// point insertion

				int index;
				float distanceToLine;

				Vector3 p = pb_Handle_Utility.ClosestPointToPolyLine(polygon.points, out index, out distanceToLine, true, polygon.transform);
				Vector3 wp = polygon.transform.TransformPoint(p);

				Vector2 ga = HandleUtility.WorldToGUIPoint(polygon.transform.TransformPoint(polygon.points[index % polygon.points.Count]));
				Vector2 gb = HandleUtility.WorldToGUIPoint(polygon.transform.TransformPoint(polygon.points[(index - 1)]));

				Vector2 mouse = Event.current.mousePosition;

				float distanceToVertex = Mathf.Min(Vector2.Distance(mouse, ga), Vector2.Distance(mouse, gb));

				if(distanceToVertex > 20f && distanceToLine < 40f)
				{
					Handles.color = Color.green;

					Handles.DotCap(-1, wp, Quaternion.identity, HandleUtility.GetHandleSize(wp) * .05f);

					if( Event.current.type == EventType.MouseDown )
					{
						pbUndo.RecordObject(polygon, "Insert Point");
						polygon.points.Insert(index, p);
						m_SelectedIndex = index;
						m_PlacingPoint = true;
						UpdateMesh(true);
					}

					Handles.color = Color.white;
				}
			}
		}

		void DoExistingPointsGUI()
		{
			Transform trs = polygon.transform;
			int len = polygon.points.Count;
			Vector3 up = polygon.transform.up;
			Vector3 right = polygon.transform.right;
			Vector3 forward = polygon.transform.forward;
			Vector3 center = Vector3.zero;

			Event evt = Event.current;

			bool used = evt.type == EventType.Used;

			if(!used && 
				(	evt.type == EventType.MouseDown &&
					evt.button == 0 &&
					!IsAppendModifier(evt.modifiers)
				)
			)
			{
				m_SelectedIndex = -1;
				Repaint();
			}

			if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Height)
			{
				if(!used && evt.type == EventType.MouseUp && evt.button == 0 && !IsAppendModifier(evt.modifiers))
					SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);
				
				bool sceneInUse = pb_Handle_Utility.SceneViewInUse(evt);

				Vector3 origin = polygon.transform.TransformPoint(pb_Math.Average(polygon.points));
				Ray r = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
				Vector3 p = sceneInUse ? origin + (polygon.extrude * up) : pb_Math.GetNearestPointRayRay(origin, trs.up, r.origin, r.direction);

				Handles.color = HANDLE_COLOR;
				Handles.DotCap(-1, origin, Quaternion.identity, HandleUtility.GetHandleSize(origin) * .05f);
				Handles.color = HANDLE_GREEN;
				Handles.DrawLine(origin, p);
				Handles.DotCap(-1, p, Quaternion.identity, HandleUtility.GetHandleSize(p) * .05f);
				Handles.color = Color.white;
				
				
				if( !sceneInUse )
				{
					float extrude = polygon.extrude;

					polygon.extrude = Vector3.Distance(origin, p) * Mathf.Sign( Vector3.Dot(p-origin, up) );

					if(polygon.extrude != extrude)
						UpdateMesh();
				}
			}
			else
			{
				// vertex dots
				for(int ii = 0; ii < len; ii++)
				{
					Vector3 point = trs.TransformPoint(polygon.points[ii]);

					center.x += point.x;
					center.y += point.y;
					center.z += point.z;

					float size = HandleUtility.GetHandleSize(point) * .05f;

					Handles.color = ii == m_SelectedIndex ? SELECTED_COLOR : HANDLE_COLOR;

					EditorGUI.BeginChangeCheck();

					point = Handles.Slider2D(point, up, right, forward, size, Handles.DotCap, Vector2.zero, true);

					if(EditorGUI.EndChangeCheck())
					{
						pbUndo.RecordObject(polygon, "Move Polygon Shape Point");
						polygon.points[ii] = trs.InverseTransformPoint(point);	
						UpdateMesh(true);
					}

					if( !used && evt.type == EventType.Used )
					{
						used = true;
						m_SelectedIndex = ii;
					}
				}

				Handles.color = Color.white;

				// height setting
				if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.Path && polygon.points.Count > 2)
				{
					center.x /= (float) len;
					center.y /= (float) len;
					center.z /= (float) len;

					Vector3 extrude = center + (up * polygon.extrude);

					EditorGUI.BeginChangeCheck();

					Handles.color = HANDLE_COLOR;
					Handles.DotCap(-1, center, Quaternion.identity, HandleUtility.GetHandleSize(center) * .05f);
					Handles.DrawLine(center, extrude);
					Handles.color = HANDLE_GREEN;
					extrude = Handles.Slider(extrude, up, HandleUtility.GetHandleSize(extrude) * .05f, Handles.DotCap, 0f);
					Handles.color = Color.white;

					if(EditorGUI.EndChangeCheck())
					{
						pbUndo.RecordObject(polygon, "Set Polygon Shape Height");
						polygon.extrude = Vector3.Distance(extrude, center) * Mathf.Sign(Vector3.Dot(up, extrude - center));
						UpdateMesh(false);
					}
				}
			}
		}

		bool IsAppendModifier(EventModifiers em)
		{
			return 	(em & EventModifiers.Shift) == EventModifiers.Shift ||
					(em & EventModifiers.Control) == EventModifiers.Control ||
					(em & EventModifiers.Alt) == EventModifiers.Alt ||
					(em & EventModifiers.Command) == EventModifiers.Command;
		}

		void HandleKeyEvent(KeyCode key, EventModifiers modifier)
		{
			switch(key)
			{
				case KeyCode.Space:
				case KeyCode.Return:
				{
					if( polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.Height);
					else if( polygon.polyEditMode == pb_PolyShape.PolyEditMode.Height )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);

					break;
				}

				case KeyCode.Backspace:
				{
					if(m_SelectedIndex > -1)
					{
						pbUndo.RecordObject(polygon, "Delete Selected Points");
						polygon.points.RemoveAt(m_SelectedIndex);
						m_SelectedIndex = -1;
						UpdateMesh();
					}
					break;
				}
			}
		}

		void DrawPolyLine(List<Vector3> points)
		{
			int vc = polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path ? points.Count : points.Count + 1;

			Vector3[] ver = new Vector3[vc];
			Vector2[] uvs = new Vector2[vc];
			int[] indices = new int[vc];

			for(int i = 0; i < vc; i++)
			{
				ver[i] = points[i%(points.Count)];
				uvs[i] = new Vector2( i / (float)(vc - 1f), 1f);
				indices[i] = i;
			}

			m_LineMesh.Clear();
			m_LineMesh.name = "Poly Shape Guide";
			m_LineMesh.vertices = ver;
			m_LineMesh.uv = uvs;
			m_LineMesh.SetIndices(indices, MeshTopology.LineStrip, 0);
		}

		void OnEditLevelChange(int editLevel)
		{
			if( polygon.polyEditMode != pb_PolyShape.PolyEditMode.None && ((EditLevel)editLevel) != EditLevel.Plugin)
				polygon.polyEditMode = pb_PolyShape.PolyEditMode.None;
		}

		void UndoRedoPerformed()
		{
			if(m_LineMesh != null)
				GameObject.DestroyImmediate(m_LineMesh);
			m_LineMesh = new Mesh();
			UpdateMesh(true);
		}
	}
}
