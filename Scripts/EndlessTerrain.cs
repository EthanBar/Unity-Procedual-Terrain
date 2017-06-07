using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {

    const float scale = 1f;

    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrviewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
    
    public LODInfo[] detailLevels;
    public static float maxViewDistance;
    public Transform viewer;
    public Material mapMat;

    public static Vector2 viewerPos;
    Vector2 viewerPosOld;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisable;

    Dictionary<Vector2, TerrainChunk> terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisableLastUpdate = new List<TerrainChunk>();

    void Start() {
        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistThreshold;
        mapGenerator = FindObjectOfType<MapGenerator>();
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisable = Mathf.RoundToInt(maxViewDistance / chunkSize);
        UpdateVisableChunks();
	}

    private void Update() {
        viewerPos = new Vector2(viewer.position.x, viewer.position.z) / scale;

        if ((viewerPosOld - viewerPos).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate) {
            viewerPosOld = viewerPos;
           UpdateVisableChunks();   
        }
    }
    void UpdateVisableChunks() {

        for (int i = 0; i < terrainChunksVisableLastUpdate.Count; i++) {
            terrainChunksVisableLastUpdate[i].SetVisable(false);
        }
        terrainChunksVisableLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPos.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPos.y / chunkSize);

        for (int yOffset = -chunksVisable; yOffset <= chunksVisable; yOffset++) {
            for (int xOffset = -chunksVisable; xOffset <= chunksVisable; xOffset++) {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDict.ContainsKey(viewedChunkCoord)) {
                    terrainChunkDict[viewedChunkCoord].UpdateTerrainChunk();

                } else {
                    terrainChunkDict.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMat));
                }
            }
        }
    }

    public class TerrainChunk {

        GameObject meshObject;
        Vector2 pos;
        Bounds bounds;
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        LODMesh collisionLODMesh;

        MapData mapData;
        bool mapDataRecieved;
        int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material) {
            pos = coord * size;
            this.detailLevels = detailLevels;
            bounds = new Bounds(pos, Vector2.one * size);
            Vector3 posV3 = new Vector3(pos.x, 0, pos.y);

            meshObject = new GameObject("Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            //meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            //meshRenderer.receiveShadows = false;
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObject.transform.position = posV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisable(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++) {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
                if (detailLevels[i].useForCollider) {
                    collisionLODMesh = lodMeshes[i];
                }
            }

            mapGenerator.RequestMapData(pos, OnMapDataRecieved);
        }

        void OnMapDataRecieved(MapData mapData) {
            this.mapData = mapData;
            mapDataRecieved = true;

            Texture2D texture = TextureGen.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }

        void OnMeshDataRecieved(MeshData meshData) {
            meshFilter.mesh = meshData.CreateMesh();
        }

        public void UpdateTerrainChunk() {
            if (mapDataRecieved) {
                float viewerDistFromEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPos));
                bool visable = viewerDistFromEdge <= maxViewDistance;

                if (visable) {
                    int lodIndex = 0;
                    for (int i = 0; i < detailLevels.Length - 1; i++) {
                        if (viewerDistFromEdge > detailLevels[i].visibleDistThreshold) {
                            lodIndex = i + 1;
                        } else {
                            break;
                        }
                    }

                    if (lodIndex != previousLODIndex) {
                        LODMesh lodmesh = lodMeshes[lodIndex];
                        if (lodmesh.hasMesh) {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodmesh.mesh;
                        } else if (!lodmesh.hasRequestedMesh) {
                            lodmesh.RequestMesh(mapData);
                        }
                    }

                    if (lodIndex == 0) {
                        if (collisionLODMesh.hasMesh) {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        } else if (!collisionLODMesh.hasRequestedMesh) {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }
                    terrainChunksVisableLastUpdate.Add(this);
                }

                SetVisable(visable);
            }
        }

        public void SetVisable(bool visable) {
            meshObject.SetActive(visable);
        }

        public bool IsVisable() {
            return meshObject.activeSelf;
        }

    }

    class LODMesh {

        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback) {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void OnMeshDataRecieved(MeshData meshData) {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData) {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataRecieved);
        }
    }

    [System.Serializable]
    public struct LODInfo {
        public int lod;
        public float visibleDistThreshold;
        public bool useForCollider;
    }
}
