import type { DayWeather } from '../api/types'
import { formatMoneyShort, formatWeekday } from '../lib/format'
import { WEATHER_COPY } from '../lib/weather'
import { WeatherIcon } from './WeatherIcon'

interface Props {
  days: DayWeather[]
  today: string
  title: string
}

export function ForecastStrip({ days, today, title }: Props) {
  return (
    <section className="card" aria-labelledby="strip-title">
      <h2 id="strip-title" className="card-title">{title}</h2>
      <ol className="strip">
        {days.map((day) => (
          <li
            key={day.date}
            className={day.date === today ? 'strip-day strip-today' : 'strip-day'}
            title={day.condition ? WEATHER_COPY[day.condition].label : 'Bütçe yok'}
          >
            <span className="strip-weekday">{day.date === today ? 'Bugün' : formatWeekday(day.date)}</span>
            <WeatherIcon condition={day.condition} size={34} />
            <span className="strip-spent">{day.spent > 0 ? formatMoneyShort(day.spent) : '—'}</span>
          </li>
        ))}
      </ol>
    </section>
  )
}
