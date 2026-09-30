import type { Weather } from '../api/types'
import { formatMoney } from '../lib/format'
import { forecastMessage } from '../lib/weather'
import { WeatherIcon } from './WeatherIcon'

export function MonthForecastCard({ weather }: { weather: Weather }) {
  const { forecast, budget } = weather
  if (!forecast || budget === null) return null

  const spentPercent = Math.min(100, (weather.spentThisMonth / budget) * 100)

  return (
    <section className="card forecast" aria-labelledby="forecast-title">
      <div className="forecast-head">
        <WeatherIcon condition={forecast.condition} size={48} />
        <div>
          <h2 id="forecast-title" className="card-title">Ay sonu tahmini</h2>
          <p className="forecast-message">{forecastMessage(forecast.condition, forecast.difference, formatMoney)}</p>
        </div>
      </div>
      <div className="month-bar" aria-hidden="true">
        <div className="month-bar-fill" style={{ width: `${spentPercent}%` }} />
      </div>
      <dl className="forecast-stats">
        <div>
          <dt>Harcanan</dt>
          <dd>{formatMoney(weather.spentThisMonth)}</dd>
        </div>
        <div>
          <dt>Kalan</dt>
          <dd className={(weather.remainingBudget ?? 0) < 0 ? 'negative' : undefined}>
            {formatMoney(weather.remainingBudget ?? 0)}
          </dd>
        </div>
        <div>
          <dt>Tahmini toplam</dt>
          <dd>{formatMoney(forecast.projectedSpend)}</dd>
        </div>
      </dl>
    </section>
  )
}
