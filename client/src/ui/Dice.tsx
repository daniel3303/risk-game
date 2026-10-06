const locations: Record<number, [number, number][]> = {
  1: [[25,25]], 2: [[13,13],[37,37]], 3: [[13,13],[25,25],[37,37]],
  4: [[13,13],[37,13],[13,37],[37,37]], 5: [[13,13],[37,13],[25,25],[13,37],[37,37]],
  6: [[13,12],[37,12],[13,25],[37,25],[13,38],[37,38]],
};

export function Dice({ value, attack = false }: { value: number; attack?: boolean }) {
  return <svg className={`dice ${attack ? 'attack-die' : ''}`} viewBox="0 0 54 58" role="img" aria-label={`Die showing ${value}`}><rect x="2" y="7" width="50" height="50" rx="10" fill={attack ? '#863e3e' : '#718894'} /><rect x="1" y="1" width="50" height="50" rx="10" fill={attack ? '#fa766d' : '#f3f8f4'} stroke={attack ? '#ffc2a4' : '#fff'} strokeWidth="2" />{locations[value]?.map(([x,y], i) => <circle key={i} cx={x} cy={y} r="3.7" fill={attack ? '#5b2027' : '#203b4c'} />)}</svg>;
}
