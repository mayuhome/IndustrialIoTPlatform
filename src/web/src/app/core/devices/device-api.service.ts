import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface DeviceStatusDto {
  deviceId: string;
  deviceCode: string;
  status: string;
  lastHeartbeatUtc: string;
  maxTemperatureThreshold: number;
}

export interface RegisterDeviceRequest {
  deviceCode: string;
  maxTemperatureThreshold: number;
}

export interface RegisterDeviceResponse {
  deviceId: string;
}

@Injectable({
  providedIn: 'root'
})
export class DeviceApiService {
  constructor(private readonly httpClient: HttpClient) {}

  getAllDevices(): Observable<DeviceStatusDto[]> {
    return this.httpClient.get<DeviceStatusDto[]>('/devices');
  }

  registerDevice(request: RegisterDeviceRequest): Observable<RegisterDeviceResponse> {
    return this.httpClient.post<RegisterDeviceResponse>('/devices/register', request);
  }
}
