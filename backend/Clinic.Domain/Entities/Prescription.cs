using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

public class Prescription : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid ConsultationId { get; private set; }
    public string? Notes { get; private set; }

    public Consultation Consultation { get; private set; } = default!;

    private readonly List<PrescriptionItem> _items = new();
    public IReadOnlyCollection<PrescriptionItem> Items => _items;

    private Prescription() { }

    public Prescription(Guid tenantId, Guid consultationId, string? notes = null)
    {
        TenantId = tenantId;
        ConsultationId = consultationId;
        Notes = notes;
    }

    public void AddItem(PrescriptionItem item) => _items.Add(item);
}
