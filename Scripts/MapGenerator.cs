using System.Collections;
using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour {

    public enum DrawMode { NoiseMap, ColorMap, Mesh};
    public Noise.NormalizeMode normalizeMode;
    public DrawMode drawMode;

    public const int mapChunkSize = 121;
    [Range(0, 6)]
    public int editorPreviewLOD;
    public float noiseScale;

    public int octaves;
    [Range(0, 1)]
    public float presistance;
    public float lacunarity;

    public bool rndSeed;
    public int seed;
    public Vector2 offset;

    public float meshHeightMulti;
    public AnimationCurve meshHeightCurve;

    public bool autoUpdate;

    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQuere = new Queue<MapThreadInfo<MeshData>>();

    void Start() {
        seed = (rndSeed) ? new System.Random().Next(-100, 100) : seed;
    }

    public void DrawMapInEditor() {
        MapData mapData = GenerateMapData(Vector2.zero);
		MapDisplay display = FindObjectOfType<MapDisplay>();
		if (drawMode == DrawMode.NoiseMap) {
            display.DrawTexture(TextureGen.TextureFromHeightMap(mapData.heightMap));
		} else if (drawMode == DrawMode.ColorMap) {
            display.DrawTexture(TextureGen.TextureFromColorMap(mapData.colorMap , mapChunkSize, mapChunkSize));
		} else if (drawMode == DrawMode.Mesh) {
            display.DrawMesh(MeshGen.GenerateTerrainMesh(mapData.heightMap, meshHeightMulti, meshHeightCurve, editorPreviewLOD), TextureGen.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
		}
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback) {
        ThreadStart threadStart = delegate {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 center, Action<MapData> callback) {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue) {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

	public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback) {
		ThreadStart threadStart = delegate {
			MeshDataThread(mapData, lod, callback);
		};
		new Thread(threadStart).Start();
	}

	void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback) {
        MeshData meshData = MeshGen.GenerateTerrainMesh(mapData.heightMap, meshHeightMulti, meshHeightCurve, lod);
        lock (meshDataThreadInfoQuere) {
            meshDataThreadInfoQuere.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void Update() {
        if (mapDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQuere.Count > 0) {
            for (int i = 0; i < meshDataThreadInfoQuere.Count; i++) {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQuere.Dequeue();
				threadInfo.callback(threadInfo.parameter);
			}
        }
    }

    public MapData GenerateMapData(Vector2 center) {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, presistance, lacunarity, center + offset, normalizeMode);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for (int y = 0; y < mapChunkSize; y++) {
            for (int x = 0; x < mapChunkSize; x++) {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++) {
                    if (currentHeight >= regions[i].height) {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                    } else {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colorMap);

    }

    private void OnValidate() {
        if (lacunarity < 1) {
            lacunarity = 1;
        }
        if (octaves < 0) {
            octaves = 0;
        }
        if (octaves > 30) {
            octaves = 30;
        }
    }

    struct MapThreadInfo<T> {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter) {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType {
    public string name;
    public float height;
    public Color color;
}

public struct MapData {
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;
    public MapData(float[,] heightMap, Color[] colorMap) {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}