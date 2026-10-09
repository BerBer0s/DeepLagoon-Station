using System.Linq;

namespace Content.Shared._DeepLagoon.Apartments;

/// <summary>Pure layout rules, shared by the server and preview. No spawning, wallet or storage dependencies.</summary>
public static class ApartmentLayout
{
    public static List<ApartmentPlacement> Resolve(ApartmentTemplatePrototype template, ApartmentDelta delta)
    {
        if (delta.SchemaVersion != 1 || delta.Template != template.ID || delta.TemplateVersion != template.Version)
            throw new InvalidOperationException("Apartment template needs migration; the saved patch was not overwritten.");
        var result = template.Furniture.Where(p => !delta.Removed.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Copy());
        foreach (var change in delta.Changed)
        {
            if (!result.TryGetValue(change.Id, out var original) || original.Furniture != change.Furniture)
                throw new InvalidOperationException("Invalid template furniture override.");
            result[change.Id] = change.Copy();
        }
        foreach (var addition in delta.Added)
        {
            if (template.Furniture.Any(p => p.Id == addition.Id) || !result.TryAdd(addition.Id, addition.Copy()))
                throw new InvalidOperationException("Duplicate apartment furniture ID.");
        }
        return result.Values.ToList();
    }

    public static ApartmentDelta Diff(ApartmentTemplatePrototype template, IReadOnlyList<ApartmentPlacement> layout, int revision)
    {
        var baseline = template.Furniture.ToDictionary(p => p.Id);
        return new ApartmentDelta
        {
            Template = template.ID, TemplateVersion = template.Version, Revision = revision,
            Removed = baseline.Keys.Where(id => layout.All(p => p.Id != id)).ToList(),
            Changed = layout.Where(p => baseline.TryGetValue(p.Id, out var original) && !p.SameAs(original)).Select(p => p.Copy()).ToList(),
            Added = layout.Where(p => !baseline.ContainsKey(p.Id)).Select(p => p.Copy()).ToList(),
        };
    }

    public static IEnumerable<Vector2i> Cells(ApartmentPlacement placement, ApartmentFurniturePrototype furniture)
    {
        var size = placement.Rotation % 2 == 0 ? furniture.Size : new Vector2i(furniture.Size.Y, furniture.Size.X);
        for (var x = 0; x < size.X; x++)
        for (var y = 0; y < size.Y; y++)
            yield return new Vector2i(placement.X + x, placement.Y + y);
    }

    public static string? Validate(ApartmentTemplatePrototype template, IReadOnlyList<ApartmentPlacement> layout,
        Func<string, ApartmentFurniturePrototype?> catalog)
    {
        if (layout.Count > template.MaxFurniture || layout.Count > 64) return "Превышен лимит мебели.";
        var ids = new HashSet<string>();
        var occupied = new HashSet<Vector2i>();
        var blocked = new HashSet<Vector2i>();
        var counts = new Dictionary<string, int>();
        foreach (var p in layout)
        {
            if (string.IsNullOrWhiteSpace(p.Id) || p.Id.Length > 64 || !ids.Add(p.Id)) return "Некорректный или повторяющийся ID предмета.";
            if (p.Rotation is < 0 or > 3) return "Некорректный поворот предмета.";
            if (!template.Catalog.Any(id => id.ToString() == p.Furniture) || catalog(p.Furniture) is not { } furniture)
                return "Предмет отсутствует в каталоге квартиры.";
            var original = template.Furniture.FirstOrDefault(item => item.Id == p.Id);
            if (original != null && original.Furniture != p.Furniture) return "Нельзя подменить предмет шаблона.";
            counts[p.Furniture] = counts.GetValueOrDefault(p.Furniture) + 1;
            if (counts[p.Furniture] > furniture.MaxCount) return "Недостаточно доступных экземпляров мебели.";
            foreach (var cell in Cells(p, furniture))
            {
                if (cell.X < 1 || cell.Y < 1 || cell.X >= template.Width - 1 || cell.Y >= template.Height - 1)
                    return "Предмет пересекается со стеной.";
                if (cell == template.Arrival || cell == template.Arrival + new Vector2i(0, 1))
                    return "Площадка прибытия и терминал должны оставаться свободными.";
                if (!occupied.Add(cell)) return "Предметы пересекаются.";
                if (furniture.BlocksMovement) blocked.Add(cell);
            }
        }
        // A valid plan must not wall off any free floor from the independent exit terminal.
        var reachable = new HashSet<Vector2i> { template.Arrival };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(template.Arrival);
        var directions = new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) };
        while (queue.TryDequeue(out var cell))
        foreach (var direction in directions)
        {
            var next = cell + direction;
            if (next.X < 1 || next.Y < 1 || next.X >= template.Width - 1 || next.Y >= template.Height - 1 || blocked.Contains(next)) continue;
            if (reachable.Add(next)) queue.Enqueue(next);
        }
        for (var x = 1; x < template.Width - 1; x++)
        for (var y = 1; y < template.Height - 1; y++)
            if (!blocked.Contains(new Vector2i(x, y)) && !reachable.Contains(new Vector2i(x, y)))
                return "Мебель перекрывает проход к выходу.";
        return null;
    }
}
