import { LoadingService } from './loading.service';

describe('LoadingService', () => {
  it('tracks concurrent requests until the final request completes', () => {
    const service = new LoadingService();
    service.begin();
    service.begin();
    expect(service.isLoading()).toBeTrue();
    service.end();
    expect(service.isLoading()).toBeTrue();
    service.end();
    expect(service.isLoading()).toBeFalse();
  });

  it('never decrements below zero', () => {
    const service = new LoadingService();
    service.end();
    expect(service.isLoading()).toBeFalse();
  });
});
