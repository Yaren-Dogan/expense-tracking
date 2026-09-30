import type { Category, WeatherCondition } from '../api/types'

interface WeatherCopy {
  label: string
  message: string
}

export const WEATHER_COPY: Record<WeatherCondition, WeatherCopy> = {
  Sunny: { label: 'Güneşli', message: 'Harika gidiyorsun, bugün bütçen ferah.' },
  PartlyCloudy: { label: 'Parçalı bulutlu', message: 'Dengeli bir gün, günlük payının içindesin.' },
  Cloudy: { label: 'Bulutlu', message: 'Günlük payının sınırındasın.' },
  Rainy: { label: 'Yağmurlu', message: 'Bugün payını aştın. Önümüzdeki günlerde toparlanabilirsin.' },
  Stormy: { label: 'Fırtına', message: 'Bugün bütçeyi fena salladın. Yarın sakin bir gün olsun.' },
}

export function forecastMessage(condition: WeatherCondition, difference: number, format: (n: number) => string) {
  const label = WEATHER_COPY[condition].label.toLocaleLowerCase('tr-TR')
  return difference >= 0
    ? `Bu hızla ay sonu ${label}. Ayı ${format(difference)} artıyla kapatırsın.`
    : `Bu hızla ay sonu ${label}. Bütçeyi ${format(-difference)} aşacaksın.`
}

export const CATEGORY_LABELS: Record<Category, string> = {
  Market: 'Market',
  Food: 'Yemek',
  Transport: 'Ulaşım',
  Bills: 'Fatura',
  Entertainment: 'Eğlence',
  Health: 'Sağlık',
  Clothing: 'Giyim',
  Other: 'Diğer',
}
