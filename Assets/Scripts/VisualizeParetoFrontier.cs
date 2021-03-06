﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Tools;
using TriangleNet.Topology;
using TriangleNet.Voronoi;
using MathUtility;

public class VisualizeParetoFrontier : MonoBehaviour
{
    public bool useDefaultLoading;

    public GameObject nodePrefab;

    public List<Vector3> fitnessList;

    public UnityEngine.Mesh uMesh;

    private List<Vector3> verticesList;

    public LineRenderer lineRenderer;

    public int[] myTriangles;

    public ParetoFrontierSO fitnessSO;


    public void Start()
    {
        Debug.Log("sup lol");
        if (useDefaultLoading)
        {
            //LoadFitnessInLocal();
            /*
                        LoadFitnessIn();
                        StartCoroutine(Generate());
              */
        }
        else
        {
            //Something with actual fitness values.
        }
    }

    public void Update()
    {
        if (!updateFrontier)
        {
            return;
        }

        UpdateFrontier();
        updateFrontier = false;
        
        if (Input.GetKeyDown(KeyCode.Return))
        {
            StopAllCoroutines();
            var transforms = transform.GetComponentsInChildren<Transform>();
            foreach (Transform trans in transforms)
            {
                if (trans.tag == "Player")
                {
                    Destroy(trans.gameObject);
                }
            }

            LoadFitnessIn();
            StartCoroutine(Generate());
        }
    }

    public static bool updateFrontier;

    public void UpdateFrontier()
    {

        StopAllCoroutines();
        var transforms = transform.GetComponentsInChildren<Transform>();
        foreach (Transform trans in transforms)
        {
            if (trans.tag == "Player")
            {
                Destroy(trans.gameObject);
            }
        }

        LoadFitnessIn();
        StartCoroutine(Generate());

        updateFrontier = false;
    }

    public void LoadFitnessIn()
    {
        List<Chromosome> paretoFront = fitnessSO.paretoFrontier;

        List<Vector3> points = new List<Vector3>();
        if (paretoFront.Count == 0)
        {
            Debug.Log("No genes in paretoFront of fitnessSO!");
            return;
        }
        points.Clear();
        fitnessList.Clear();
        foreach (Chromosome gene in paretoFront)
        {
            //gene.track1 time converted to coordinate
            //gene.track2 time converted to coordinate.
            //etc.
            //Use MathUtilities map() - max time taken is 30, min-time is 0.

            //Order decides how track-fitness is visualized in coordinate-space.
            float x = gene.bestTrack1;
            float y = gene.bestTrack2;
            float z = gene.bestTrack3;

            y = MathUtils.map(y, 0, Handler.timePerSimulation, 0, 30);

            Vector3 newPoint = new Vector3(30 - x, 30 - y, 30 - z);
            points.Add(newPoint);
        }

        fitnessList = points;
        //Something with actual fitness values.
    }


    private IEnumerator Generate()
    {
        float xMax, yMax, zMax;
        xMax = yMax = zMax = 0;

        foreach (Vector3 point in fitnessList)
        {
            xMax = Mathf.Max(xMax, point.x);
            yMax = Mathf.Max(yMax, point.y);
            zMax = Mathf.Max(zMax, point.z);
            GameObject go = Instantiate(nodePrefab, point + transform.position, quaternion.identity, transform);
            ParetoNodeInfo paretoNodeInfo = go.AddComponent<ParetoNodeInfo>();

            paretoNodeInfo.bestTrack1 = Mathf.Abs(point.x - 30);
            paretoNodeInfo.bestTrack2 = Mathf.Abs(point.y - 30);
            paretoNodeInfo.bestTrack3 = Mathf.Abs(point.z - 30);
            yield return new WaitForSeconds(0.04f);
        }

        GetComponent<MeshFilter>().mesh = uMesh = new UnityEngine.Mesh();
        uMesh.name = "Pareto Mesh";

        vertices = fitnessList.ToArray();
        Debug.Log("Vertices: " + vertices.ToString());
        MakeMesh(vertices);


        uMesh.RecalculateNormals();
    }
    public Vector3[] vertices;

    public Transform chunkPrefab;
    TriangleNet.Mesh mesh;
    private List<float> elevations = new List<float>();

    public void MakeMesh(Vector3[] _vertices)
    {
        Polygon polygon = new Polygon();
        elevations.Clear();

        for (int i = 0; i < _vertices.Length; i++)
        {
            polygon.Add(new Vertex(_vertices[i].x, _vertices[i].z));
            elevations.Add(_vertices[i].y);
        }

        Debug.Log("Length of polygon vertex-count: " + polygon.Count +
         "\n Length of elevations:" + elevations.Count);


        int trianglesInChunk = 400;

        TriangleNet.Meshing.ConstraintOptions options = new TriangleNet.Meshing.ConstraintOptions() { ConformingDelaunay = true, Convex = false, SegmentSplitting = 1 };
        mesh = (TriangleNet.Mesh)polygon.Triangulate(options);

        /*
              foreach (Vertex vert in mesh.Vertices)
              {
                  elevations.Add(7);
              }
      */
        Debug.Log("Mesh.vertices at breakpoint: " + mesh.Vertices.Count);






        // Instantiate an enumerator to go over the Triangle.Net triangles - they don't
        // provide any array-like interface for indexing
        IEnumerator<Triangle> triangleEnumerator = mesh.Triangles.GetEnumerator();


        // Create more than one chunk, if necessary
        for (int chunkStart = 0; chunkStart < mesh.Triangles.Count; chunkStart += trianglesInChunk)
        {
            // Vertices in the unity mesh
            List<Vector3> vertices = new List<Vector3>();

            // Per-vertex normals
            List<Vector3> normals = new List<Vector3>();

            // Per-vertex UVs - unused here, but Unity still wants them
            List<Vector2> uvs = new List<Vector2>();

            // Triangles - each triangle is made of three indices in the vertices array
            List<int> triangles = new List<int>();

            // Iterate over all the triangles until we hit the maximum chunk size
            int chunkEnd = chunkStart + trianglesInChunk;
            for (int i = chunkStart; i < chunkEnd; i++)
            {
                if (!triangleEnumerator.MoveNext())
                {
                    // If we hit the last triangle before we hit the end of the chunk, stop
                    break;
                }

                // Get the current triangle
                Triangle triangle = triangleEnumerator.Current;

                // For the triangles to be right-side up, they need
                // to be wound in the opposite direction
                Vector3 v0 = GetPoint3D(triangle.vertices[2].id);
                Vector3 v1 = GetPoint3D(triangle.vertices[1].id);
                Vector3 v2 = GetPoint3D(triangle.vertices[0].id);

                // This triangle is made of the next three vertices to be added
                triangles.Add(vertices.Count);
                triangles.Add(vertices.Count + 1);
                triangles.Add(vertices.Count + 2);
                triangles.Add(vertices.Count);
                triangles.Add(vertices.Count + 2);
                triangles.Add(vertices.Count + 1);

                // Add the vertices
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);

                // Compute the normal - flat shaded, so the vertices all have the same normal
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                // If you want to texture your terrain, UVs are important,
                // but I just use a flat color so put in dummy coords
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.0f));
            }

            // Create the actual Unity mesh object
            UnityEngine.Mesh chunkMesh = new UnityEngine.Mesh();
            chunkMesh.vertices = vertices.ToArray();
            chunkMesh.uv = uvs.ToArray();
            chunkMesh.triangles = triangles.ToArray();
            chunkMesh.normals = normals.ToArray();

            // Instantiate the GameObject which will display this chunk
            Transform chunk = Instantiate<Transform>(chunkPrefab, transform.position, transform.rotation);
            chunk.GetComponent<MeshFilter>().mesh = chunkMesh;
            //chunk.GetComponent<MeshCollider>().sharedMesh = chunkMesh;
            chunk.transform.parent = transform;
        }
    }

    // This method returns the world-space vertex for a given vertex index
    public Vector3 GetPoint3D(int index)
    {
        Vertex vertex = mesh.vertices[index];
        float elevation = elevations[index];
        Debug.Log("Length of mesh.vertices: " + mesh.vertices.Count +
                "\n Length of elevations:" + elevations.Count);

        return new Vector3((float)vertex.x, elevation, (float)vertex.y);
    }




    IEnumerator CreateTriangles(Vector3[] vertices)
    {
        foreach (Vector3 pivot in vertices)
        {
            Vector3 closestPoint = new Vector3();


            foreach (Vector3 focus in vertices)
            {
                if (focus == pivot)
                {
                    continue;
                }



            }
        }

        yield return new WaitForSeconds(0.1f);

    }


    struct ModAdd
    {
        private int value;

        public int pp(int step, int cap)
        {
            return (value + step) % cap;
        }

        public int GetValue()
        {
            return value;
        }

        public ModAdd(int _value)
        {
            value = _value;
        }
    }

    public Vector3 DirFromAtoB(Vector3 a, Vector3 b)
    {
        return -a + b;
    }
}
