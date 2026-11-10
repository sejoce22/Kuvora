import { formatClp } from './clp';

describe('formatClp', () => {
  it('usa punto como separador de miles y no muestra decimales', () => {
    expect(formatClp(3500)).toContain('3.500');
    expect(formatClp(3500)).not.toContain(',');
  });

  it('muestra el cero', () => {
    expect(formatClp(0)).toContain('0');
  });

  it('agrupa los millones', () => {
    expect(formatClp(1250000)).toContain('1.250.000');
  });
});
