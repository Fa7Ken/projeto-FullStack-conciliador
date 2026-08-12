import { TestBed } from '@angular/core/testing';

import { Conciliacao } from './conciliacao';

describe('Conciliacao', () => {
  let service: Conciliacao;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Conciliacao);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
