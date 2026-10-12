// Iterative Ramer–Douglas–Peucker simplification keeps endpoints and limits wire payloads.
export function simplifyPoints(points, limit = 128) {
  if (points.length <= limit) return points;
  let tolerance = .0005;
  for (let attempt = 0; attempt < 20; attempt++, tolerance *= 1.6) {
    const keep = new Set([0, points.length - 1]);
    const ranges = [[0, points.length - 1]];
    while (ranges.length) {
      const [first, last] = ranges.pop();
      const a = points[first], b = points[last], dx = b[0] - a[0], dy = b[1] - a[1];
      const length = dx * dx + dy * dy;
      let largest = tolerance * tolerance, index = -1;
      for (let i = first + 1; i < last; i++) {
        const p = points[i];
        const t = length ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / length)) : 0;
        const distance = (p[0] - a[0] - t * dx) ** 2 + (p[1] - a[1] - t * dy) ** 2;
        if (distance > largest) { largest = distance; index = i; }
      }
      if (index >= 0) { keep.add(index); ranges.push([first, index], [index, last]); }
    }
    if (keep.size <= limit) return [...keep].sort((a, b) => a - b).map(i => points[i]);
  }
  return Array.from({ length: limit }, (_, i) => points[Math.round(i * (points.length - 1) / (limit - 1))]);
}
