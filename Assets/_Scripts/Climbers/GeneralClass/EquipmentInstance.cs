using UnityEngine;

public abstract class EquipmentInstance
{
    protected string equipmentName;

    protected ClimberLoadout ownerLoadout;
    

    /// <summary>
    /// Se llama desde ClimberLoadout justo después de crear la instancia.
    /// Sirve para que el equipo sepa a qué escalador pertenece.
    /// </summary>
    public virtual void SetupOwner(ClimberLoadout loadout)
    {
        ownerLoadout = loadout;

        if (ownerLoadout != null)
        {
            var go = ownerLoadout.gameObject;

        }
    }

    public ClimberLoadout OwnerLoadout => ownerLoadout;

    public virtual void Initialize(string name)
    {
        equipmentName = name;
        Debug.Log($"[Equipment] {equipmentName} initialized.");
    }

    public virtual void OnEncounterObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"[Equipment] {equipmentName} encountered obstacle of type {obstacleType}.");
    }

    public virtual void OnCounterSuccess(ObstacleType type)
    {
        Debug.Log($"[Equipment] {equipmentName} successfully countered obstacle {type}.");
    }

    public virtual void OnCounterFail(ObstacleType type)
    {
        Debug.Log($"[Equipment] {equipmentName} failed to counter obstacle {type}.");
    }

    public virtual void OnExitObstacle(ObstacleType type)
    {
        // Override aquí si ese equipo debe hacer algo al salir
    }

    /// <summary>
    /// Indica si este equipo puede manejar / contrarrestar
    /// un obstáculo de cierto tipo a nivel de grafo.
    /// Por defecto, ninguno.
    /// </summary>
    public virtual bool CanHandleObstacle(ObstacleType obstacleType)
    {
        return false;
    }
}
