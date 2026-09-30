import type { WeatherCondition } from '../api/types'

interface Props {
  condition: WeatherCondition | null
  size?: number
  animated?: boolean
}

const CLOUD = 'M20 46h26a11 11 0 0 0 0-22 15 15 0 0 0-28.6 4.5A9 9 0 0 0 20 46z'

function Sun({ cx, cy, r, animated }: { cx: number; cy: number; r: number; animated: boolean }) {
  const rays = Array.from({ length: 8 }, (_, i) => (i * Math.PI) / 4)
  return (
    <g className={animated ? 'wi-spin' : undefined} style={{ transformOrigin: `${cx}px ${cy}px` }}>
      <circle cx={cx} cy={cy} r={r} fill="var(--wi-sun)" />
      {rays.map((a) => (
        <line
          key={a}
          x1={cx + Math.cos(a) * (r + 4)}
          y1={cy + Math.sin(a) * (r + 4)}
          x2={cx + Math.cos(a) * (r + 9)}
          y2={cy + Math.sin(a) * (r + 9)}
          stroke="var(--wi-sun)"
          strokeWidth={3}
          strokeLinecap="round"
        />
      ))}
    </g>
  )
}

function Cloud({ dx = 0, dy = 0, fill = 'var(--wi-cloud)', drift = false }) {
  // Offset on the group: a CSS animation on the path itself would override an SVG transform attribute.
  return (
    <g transform={`translate(${dx} ${dy})`}>
      <path d={CLOUD} fill={fill} className={drift ? 'wi-drift' : undefined} />
    </g>
  )
}

function Drops({ animated }: { animated: boolean }) {
  return (
    <g stroke="var(--wi-rain)" strokeWidth={3} strokeLinecap="round">
      {[22, 32, 42].map((x, i) => (
        <line
          key={x}
          x1={x}
          y1={50}
          x2={x - 3}
          y2={58}
          className={animated ? 'wi-drop' : undefined}
          style={{ animationDelay: `${i * 0.25}s` }}
        />
      ))}
    </g>
  )
}

/** Hand-drawn weather glyphs; animation is disabled by CSS when the user prefers reduced motion. */
export function WeatherIcon({ condition, size = 64, animated = false }: Props) {
  const label = condition ?? 'Unknown'
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" role="img" aria-label={label} className="weather-icon">
      {condition === 'Sunny' && <Sun cx={32} cy={32} r={12} animated={animated} />}
      {condition === 'PartlyCloudy' && (
        <>
          <Sun cx={24} cy={22} r={9} animated={animated} />
          <Cloud dx={4} dy={4} drift={animated} />
        </>
      )}
      {condition === 'Cloudy' && (
        <>
          <Cloud dx={8} dy={-8} fill="var(--wi-cloud-back)" />
          <Cloud dx={-2} dy={2} drift={animated} />
        </>
      )}
      {condition === 'Rainy' && (
        <>
          <Cloud dy={-6} fill="var(--wi-cloud-rain)" />
          <Drops animated={animated} />
        </>
      )}
      {condition === 'Stormy' && (
        <>
          <Cloud dy={-6} fill="var(--wi-cloud-storm)" />
          <path
            d="M33 38l-7 12h6l-3 10 10-14h-6l4-8z"
            fill="var(--wi-bolt)"
            className={animated ? 'wi-flash' : undefined}
          />
        </>
      )}
      {condition === null && (
        <path d={CLOUD} fill="none" stroke="var(--wi-cloud)" strokeWidth={2.5} strokeDasharray="4 4" />
      )}
    </svg>
  )
}
