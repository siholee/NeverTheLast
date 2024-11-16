using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    public int gridRadius = 3; // Radius of the hex grid in hex coordinates
    public float hexRadius = 1f; // Radius of each hex cell
    public GameObject hexPrefab; // Prefab for the hex cell, using your provided image

    private float hexWidth;
    private float hexHeight;

    void Start()
    {
        CalculateHexDimensions();
        CreateHexGrid();
    }

    // Calculate the width and height of the hex based on its radius
    void CalculateHexDimensions()
    {
        hexWidth = Mathf.Sqrt(3) * hexRadius;
        hexHeight = 2 * hexRadius;
    }

    // Create the hex grid using axial coordinates (r, q, s)
    void CreateHexGrid()
    {
        for (int r = -gridRadius; r <= gridRadius; r++)
        {
            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                int s = -r - q;
                if (Mathf.Abs(s) <= gridRadius)
                {
                    Vector3 position = CalculateWorldPosition(r, q);
                    GameObject hex = Instantiate(hexPrefab, position, Quaternion.identity, transform);
                    hex.transform.Rotate(90, 0, 0); // Rotate the prefab 90 degrees around the x-axis
                }
            }
        }
    }

    // Converts hex coordinates (r, q) to world position
    Vector3 CalculateWorldPosition(int r, int q)
    {
        float x = hexRadius * Mathf.Sqrt(3) * (q + r / 2f);
        float z = hexRadius * 3f / 2f * r;
        return new Vector3(x, 0, z);
    }
}