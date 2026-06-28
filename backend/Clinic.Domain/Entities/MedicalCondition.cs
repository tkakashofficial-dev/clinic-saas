using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

// This is a lookup/seed table
// Contains: Diabetes, Hypertension, DrugAllergy, LatexAllergy, Pregnancy etc
// These are fixed values — not per tenant
public class MedicalCondition : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;

    private MedicalCondition() { }

    public MedicalCondition(string name, string code)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }
}