import type { Weather } from '../api/types'
import { formatMoney, formatMonth } from '../lib/format'
import { WEATHER_COPY } from '../lib/weather'
import { WeatherIcon } from './WeatherIcon'

interface Props {
  weather: Weather
  onEditBudget: () => void
}

export function WeatherHero({ weather, onEditBudget }: Props) {
  const today = weather.today
  const condition = today?.condition ?? null

  if (weather.budget === null || today === null) {
    return (
      <section className="hero" data-sky="none">
        <p className="hero-kicker">{formatMonth(weather.month)}</p>
        <div className="hero-main">
          <WeatherIcon condition={null} size={96} />
          <div>
            <h1 className="hero-title">Hava durumu için bütçe lazım</h1>
            <p className="hero-message">Bu ay ne kadar harcamak istediğini söyle, her günün havasını hesaplayalım.</p>
          </div>
        </div>
        <button type="button" className="button button-light" onClick={onEditBudget}>
          Aylık bütçeni belirle
        </button>
      </section>
    )
  }

  const copy = WEATHER_COPY[today.condition]
  // Cap the bar at 100% so a stormy day reads as "full", not as an overflowing element.
  const usedPercent = today.dailyAllowance > 0 ? Math.min(100, (today.spentToday / today.dailyAllowance) * 100) : 100

  return (
    <section className="hero" data-sky={condition}>
      <div className="hero-top">
        <p className="hero-kicker">Bugün · {formatMonth(weather.month)}</p>
        <button type="button" className="link-light" onClick={onEditBudget}>
          Bütçe: {formatMoney(weather.budget)}
        </button>
      </div>

      <div className="hero-main">
        <WeatherIcon condition={condition} size={112} animated />
        <div>
          <h1 className="hero-title">{copy.label}</h1>
          <p className="hero-message">{copy.message}</p>
        </div>
      </div>

      <div className="hero-safe">
        <span className="hero-safe-label">Gönül rahatlığıyla harcayabileceğin</span>
        <strong className="hero-safe-value">{formatMoney(today.safeToSpend)}</strong>
      </div>

      <div
        className="allowance"
        role="meter"
        aria-label="Günlük payın ne kadarı kullanıldı"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(usedPercent)}
      >
        <div className="allowance-fill" style={{ width: `${usedPercent}%` }} />
      </div>
      <p className="hero-footnote">
        Bugün {formatMoney(today.spentToday)} harcadın · günlük payın {formatMoney(today.dailyAllowance)}
      </p>
    </section>
  )
}
