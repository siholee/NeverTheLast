using UnityEngine;

public class Cell : MonoBehaviour
{
    public int xPos;
    public int yPos;
    public int zPos;

    // New property to check if the cell is occupied
    private bool isOccupied = false;

    // Public property to get or set if the cell is occupied
    public bool IsOccupied
    {
        get { return isOccupied; }
        set { isOccupied = value; }
    }
}