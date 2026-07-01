import {
  AfterViewInit,
  Component,
  computed,
  ElementRef,
  OnDestroy,
  ViewChild,
  effect,
  inject
} from '@angular/core';
import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import * as THREE from 'three';
import { DeviceStoreService } from '../../core/state/device-store.service';
import { LiveDeviceTelemetry } from '../../core/realtime/simulation-data.model';

@Component({
  selector: 'app-showroom-device',
  standalone: true,
  imports: [DecimalPipe, DatePipe, NgClass],
  templateUrl: './device.html',
  styleUrl: './device.css',
})
export class Device implements AfterViewInit, OnDestroy {
  @ViewChild('sceneHost', { static: true })
  protected readonly sceneHost?: ElementRef<HTMLDivElement>;

  private readonly store = inject(DeviceStoreService);

  protected readonly devices = this.store.devices;
  protected readonly selectedDevice = this.store.selectedDevice;
  protected readonly selectedDeviceId = computed(() => this.selectedDevice()?.deviceId ?? '');
  protected readonly connectionState = this.store.connectionState;

  private renderer?: THREE.WebGLRenderer;
  private scene?: THREE.Scene;
  private camera?: THREE.PerspectiveCamera;
  private animationFrameId: number | undefined;
  private resizeFrameId: number | undefined;
  private resizeObserver?: ResizeObserver;
  private lastWidth = 0;
  private lastHeight = 0;
  private readonly meshesByDeviceId = new Map<string, THREE.Mesh>();

  private readonly syncMeshesEffect = effect(() => {
    this.synchronizeMeshes(this.devices());
  });

  ngAfterViewInit(): void {
    const host = this.sceneHost?.nativeElement;
    if (!host) {
      return;
    }

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color('#051417');

    this.camera = new THREE.PerspectiveCamera(55, 1, 0.1, 120);
    this.camera.position.set(0, 8, 18);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    host.appendChild(this.renderer.domElement);

    const ambient = new THREE.AmbientLight(0x88b8bf, 0.7);
    this.scene.add(ambient);

    const keyLight = new THREE.DirectionalLight(0xf7b733, 1.1);
    keyLight.position.set(7, 12, 5);
    this.scene.add(keyLight);

    const fillLight = new THREE.DirectionalLight(0x5ae7ff, 0.7);
    fillLight.position.set(-6, 5, -4);
    this.scene.add(fillLight);

    const floorGeometry = new THREE.CircleGeometry(13, 80);
    const floorMaterial = new THREE.MeshStandardMaterial({ color: 0x0f2a2f, roughness: 0.86 });
    const floor = new THREE.Mesh(floorGeometry, floorMaterial);
    floor.rotateX(-Math.PI / 2);
    floor.position.y = -2;
    this.scene.add(floor);

    this.resizeObserver = new ResizeObserver(() => {
      if (this.resizeFrameId) {
        cancelAnimationFrame(this.resizeFrameId);
      }

      this.resizeFrameId = requestAnimationFrame(() => {
        this.resizeRenderer();
      });
    });
    this.resizeObserver.observe(host);
    this.resizeRenderer();
    this.renderFrame();
  }

  ngOnDestroy(): void {
    if (this.animationFrameId) {
      cancelAnimationFrame(this.animationFrameId);
    }

    if (this.resizeFrameId) {
      cancelAnimationFrame(this.resizeFrameId);
    }

    this.resizeObserver?.disconnect();
    this.renderer?.dispose();
    this.syncMeshesEffect.destroy();
  }

  protected selectDevice(deviceId: string): void {
    this.store.selectDevice(deviceId);
  }

  private resizeRenderer(): void {
    const host = this.sceneHost?.nativeElement;
    if (!host || !this.renderer || !this.camera) {
      return;
    }

    const width = host.clientWidth;
    const height = host.clientHeight;

    if (width === 0 || height === 0) {
      return;
    }

    if (width === this.lastWidth && height === this.lastHeight) {
      return;
    }

    this.lastWidth = width;
    this.lastHeight = height;

    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
  }

  private renderFrame = (): void => {
    this.animationFrameId = requestAnimationFrame(this.renderFrame);

    if (!this.scene || !this.camera || !this.renderer) {
      return;
    }

    const elapsed = performance.now() * 0.00025;
    this.camera.position.x = Math.sin(elapsed) * 2.1;
    this.camera.lookAt(0, 0, 0);

    for (const mesh of this.meshesByDeviceId.values()) {
      mesh.rotation.y += 0.01;
    }

    this.renderer.render(this.scene, this.camera);
  };

  private synchronizeMeshes(devices: ReadonlyArray<LiveDeviceTelemetry>): void {
    if (!this.scene) {
      return;
    }
    const scene = this.scene;

    const incomingIds = new Set(devices.map((device) => device.deviceId));

    for (const [deviceId, mesh] of this.meshesByDeviceId.entries()) {
      if (incomingIds.has(deviceId)) {
        continue;
      }

      scene.remove(mesh);
      mesh.geometry.dispose();
      const material = mesh.material;
      if (material instanceof THREE.Material) {
        material.dispose();
      }
      this.meshesByDeviceId.delete(deviceId);
    }

    const radius = 5;
    devices.forEach((device, index) => {
      const theta = (index / Math.max(devices.length, 1)) * Math.PI * 2;
      const x = Math.cos(theta) * radius;
      const z = Math.sin(theta) * radius;
      const y = -0.4 + Math.sin(theta * 1.6) * 0.35;
      const color = this.colorByStatus(device.status);

      const mesh = this.meshesByDeviceId.get(device.deviceId);
      if (mesh) {
        mesh.position.set(x, y, z);
        const material = mesh.material;
        if (material instanceof THREE.MeshStandardMaterial) {
          material.color.setHex(color);
          material.emissive.setHex(color);
        }
        return;
      }

      const geometry = new THREE.BoxGeometry(1.25, 2.2, 1.25);
      const material = new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: 0.1,
        roughness: 0.44,
        metalness: 0.16
      });
      const newMesh = new THREE.Mesh(geometry, material);
      newMesh.position.set(x, y, z);
      scene.add(newMesh);
      this.meshesByDeviceId.set(device.deviceId, newMesh);
    });
  }

  private colorByStatus(status: string): number {
    const key = status.toLowerCase();
    if (key.includes('maintenance')) {
      return 0xff9f1c;
    }
    if (key.includes('stop') || key.includes('offline')) {
      return 0xff5b5b;
    }
    return 0x2de39b;
  }
}
