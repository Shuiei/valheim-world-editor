using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
	// dungeon-rooms.json.gz: what the editor's Dungeon mode needs of the game's dungeons. A dungeon
	// (DG_Cave, DG_SunkenCrypt...) is a DungeonGenerator object whose saved "roomData" lists rooms by the
	// stable hash of their prefab name, with position and rotation; the game rebuilds them from that
	// list. For each dungeon kind: its algorithm, room themes, the box its rooms must stay in (m_zoneSize,
	// around the zone centre at the dungeon's height), its door types ([prefab, opening type, chance (0:
	// the dungeon's doorChance)]) and baseSeed (its seed is added to each room's roll). For each room
	// prefab (a root with a Room component):
	//   theme, size, endCap, entrance, divider, endCapPrio, weight, perimeter, enabled;
	//   openings (RoomConnection children): [type, entrance, allowDoor, onlyIfOtherAllowsDoor, x, y, z, qx,
	//     qy, qz, qw] in the room's frame (the game joins two rooms where openings meet, the new room
	//     turned so its opening faces the other);
	//   contents: the networked objects (ZNetView children) the game makes when it first generates the
	//     room: [prefab, x, y, z, qx, qy, qz, qw, node], and what decides whether each is there, in the
	//     order the game rolls them: spawns (RandomSpawn: [node, chance %, required theme, off node, min
	//     elevation, max elevation]) and picks (RandomObject: [node, required theme, [[choice node,
	//     weight]...], min elevation, max elevation]), with nodes: the parent of every node that matters
	//     (-1 the room).
	// Only what is active and enabled in the prefab counts (as the game's GetEnabledComponentsInChildren).
	private sealed class DungeonScan : IScan
	{
		private readonly Dictionary<string, Dict> _dungeons = new(), _rooms = new();

		public Dict Json()
		{
			static Dict Sorted(Dictionary<string, Dict> d)
			{
				var res = new Dict();
				foreach (var (k, v) in d.OrderBy(kv => kv.Key, StringComparer.Ordinal))
				{
					res[k] = v;
				}
				return res;
			}
			return new Dict { ["dungeons"] = Sorted(_dungeons), ["rooms"] = Sorted(_rooms) };
		}

		private static string? Kind(AssetTypeValueField f) =>
			Has(f, "m_theme") && Has(f, "m_endCap") && Has(f, "m_size") ? "room"
			: Has(f, "m_entrance") && Has(f, "m_allowDoor") ? "connection"
			: Has(f, "m_persistent") && Has(f, "m_distant") ? "netview"
			: Has(f, "m_chanceToSpawn") && Has(f, "m_OffObject") ? "spawn"
			: Has(f, "m_objects") && Has(f, "m_dungeonRequireTheme") && !Has(f, "m_chanceToSpawn") ? "pick"
			: Has(f, "m_algorithm") && Has(f, "m_themes") && Has(f, "m_maxRooms") ? "generator"
			: null;

		// A GameObject's Transform and its MonoBehaviours that can be read.
		private static (AssetTypeValueField? Tr, List<AssetTypeValueField> Behaviours) Parts(Bundle b, AssetTypeValueField go)
		{
			AssetTypeValueField? tr = null;
			var behaviours = new List<AssetTypeValueField>();
			foreach (var c in go["m_Component.Array"].Children)
			{
				long pid = c["component.m_PathID"].AsLong;
				int t = b.TypeOf(pid);
				if (t == TransformType)
				{
					tr = b.Field(pid);
				}
				else if (t == MonoBehaviourType && b.Field(pid) is { } f)
				{
					behaviours.Add(f);
				}
			}
			return (tr, behaviours);
		}

		private static readonly System.Buffers.SearchValues<char> NameEnd = System.Buffers.SearchValues.Create("( ");

		// Name up to the first '(' or space (the game's Utils.GetPrefabName).
		private static string PrefabName(string name)
		{
			int i = name.AsSpan().IndexOfAny(NameEnd);
			return i < 0 ? name : name[..i];
		}

		public void Read(Bundle b)
		{
			foreach (var (_, info) in b.Objects)
			{
				if (info.TypeId != GameObjectType || b.Field(info.PathId) is not { } go)
				{
					continue;
				}
				var (tr, behaviours) = Parts(b, go);
				if (tr == null || tr["m_Father.m_PathID"].AsLong != 0)
				{
					continue;
				}
				var kinds = new Dictionary<string, AssetTypeValueField>();
				foreach (var f in behaviours)
				{
					if (Kind(f) is string k)
					{
						kinds[k] = f;
					}
				}
				string name = go["m_Name"].AsString;
				if (kinds.TryGetValue("generator", out var g))
				{
					var zone = Vec(g["m_zoneSize"]);
					_dungeons[name] = new Dict
					{
						["algorithm"] = Py(g["m_algorithm"]),
						["themes"] = Py(g["m_themes"]),
						["maxRooms"] = Py(g["m_maxRooms"]),
						["minRooms"] = Py(g["m_minRooms"]),
						["tileWidth"] = Round(Get(g, "m_tileWidth", 8L), 4),
						["zoneSize"] = new List<object?> { R(zone.X, 4), R(zone.Y, 4), R(zone.Z, 4) },
						["customInterior"] = Int(Get(g, "m_useCustomInteriorTransform", 0L)),
						["baseSeed"] = Int(Get(g, "m_addBaseSeedToRandomSpawn", 0L)),
						["doorChance"] = Round(Get(g, "m_doorChance", 0.5), 4),
						["doors"] = Doors(b, g),
					};
				}
				if (kinds.TryGetValue("room", out var room) && !_rooms.ContainsKey(name))
				{
					_rooms[name] = new RoomReader(b, info.PathId).Read(room);
				}
			}
		}

		private static List<object?> Doors(Bundle b, AssetTypeValueField g)
		{
			var res = new List<object?>();
			if (!Has(g, "m_doorTypes"))
			{
				return res;
			}
			foreach (var d in g["m_doorTypes.Array"].Children)
			{
				var pptr = d["m_prefab"];
				long pid = pptr["m_PathID"].AsLong;
				if (pptr["m_FileID"].AsInt == 0 && pid != 0 && b.Field(pid) is { } go && Has(go, "m_Name"))
				{
					res.Add(new List<object?> { go["m_Name"].AsString, Py(d["m_connectionType"]), Round(Get(d, "m_chance", 0L), 4) });
				}
			}
			return res;
		}

		// One room prefab's openings, contents and random parts.
		private sealed class RoomReader
		{
			private readonly Bundle _b;
			private readonly long _root;
			// Node of each GameObject that matters (numbered as first named), and every object's parent.
			private readonly Dictionary<long, long> _node = new(), _parent = new();
			private readonly List<object?> _nodes = new(), _openings = new(), _contents = new(), _spawns = new(), _picks = new();

			public RoomReader(Bundle b, long root)
			{
				_b = b;
				_root = root;
			}

			public Dict Read(AssetTypeValueField room)
			{
				var info = new Dict
				{
					["theme"] = Py(room["m_theme"]),
					["size"] = new List<object?> { Py(room["m_size.x"]), Py(room["m_size.y"]), Py(room["m_size.z"]) },
					["endCap"] = Int(Py(room["m_endCap"])),
					["entrance"] = Int(Py(room["m_entrance"])),
					["divider"] = Int(Get(room, "m_divider", 0L)),
					["endCapPrio"] = Get(room, "m_endCapPrio", 0L),
					["weight"] = Round(Get(room, "m_weight", 1L), 4),
					["perimeter"] = Int(Get(room, "m_perimeter", 0L)),
					["enabled"] = Int(Get(room, "m_enabled", 1L)),
					["openings"] = _openings,
					["contents"] = _contents,
					["spawns"] = _spawns,
					["picks"] = _picks,
					["nodes"] = _nodes,
				};
				// Parents first, so a node can be numbered when a random part names it before the walk gets there.
				Index(_root);
				Walk(_root, default, Quat.Root, true);
				return info;
			}

			private long Node(long pid)
			{
				if (pid == _root)
				{
					return -1;
				}
				if (!_node.TryGetValue(pid, out long n))
				{
					long p = Node(_parent[pid]);
					_node[pid] = n = _nodes.Count;
					_nodes.Add(p);
				}
				return n;
			}

			private IEnumerable<long> Children(AssetTypeValueField? tr) => tr == null ? Enumerable.Empty<long>() : ChildObjects(_b, tr);

			private void Index(long gid)
			{
				if (_b.Field(gid) is not { } go)
				{
					return;
				}
				foreach (long child in Children(Parts(_b, go).Tr))
				{
					_parent[child] = gid;
					Index(child);
				}
			}

			// Depth first, in child order: the order the game finds the components in, so the order it
			// rolls the random parts in. Inactive objects and their children are left out.
			private void Walk(long gid, V3 pos, Quat rot, bool root)
			{
				if (_b.Field(gid) is not { } g)
				{
					return;
				}
				var (t, behaviours) = Parts(_b, g);
				if (t == null)
				{
					return;
				}
				if (!root)
				{
					if (!Truthy(Get(g, "m_IsActive", true)))
					{
						return;
					}
					pos = Add(pos, rot.Rotate(Vec(t["m_LocalPosition"])));
					rot = rot.Mul(Quat.Of(t["m_LocalRotation"]));
				}
				List<object?> Place() => root ? Origin().Concat(rot.Rounded(4)).ToList() : new object?[] { R(pos.X, 4), R(pos.Y, 4), R(pos.Z, 4) }.Concat(rot.Rounded(4)).ToList();
				foreach (var b in behaviours)
				{
					if (!Truthy(Get(b, "m_Enabled", 1L)))
					{
						continue;
					}
					string? k = Kind(b);
					if (root && k != "connection")
					{
						continue;
					}
					switch (k)
					{
						case "connection":
							_openings.Add(new List<object?> { Py(b["m_type"]), Int(Py(b["m_entrance"])), Int(Py(b["m_allowDoor"])), Int(Get(b, "m_doorOnlyIfOtherAlsoAllowsDoor", 0L)) }.Concat(Place()).ToList());
							break;
						case "netview":
							_contents.Add(new List<object?> { PrefabName(g["m_Name"].AsString) }.Concat(Place()).Append(Node(gid)).ToList());
							break;
						case "spawn":
							long off = b["m_OffObject.m_PathID"].AsLong;
							long node = Node(gid);
							_spawns.Add(new List<object?> { node, Round(Py(b["m_chanceToSpawn"]), 4), Get(b, "m_dungeonRequireTheme", 0L), off != 0 && _parent.ContainsKey(off) ? Node(off) : -1L, Get(b, "m_minElevation", -10000L), Get(b, "m_maxElevation", 10000L) });
							break;
						case "pick":
							var choices = new List<object?>();
							foreach (var e in b["m_objects.Array"].Children)
							{
								long cid = e["m_object.m_PathID"].AsLong;
								if (cid != 0 && _parent.ContainsKey(cid))
								{
									choices.Add(new List<object?> { Node(cid), Round(Py(e["m_weight"]), 4) });
								}
							}
							_picks.Add(new List<object?> { Node(gid), Get(b, "m_dungeonRequireTheme", 0L), choices, Get(b, "m_minElevation", -10000L), Get(b, "m_maxElevation", 10000L) });
							break;
					}
				}
				foreach (long child in Children(t))
				{
					_parent[child] = gid;
					Walk(child, pos, rot, false);
				}
			}
		}
	}
}
