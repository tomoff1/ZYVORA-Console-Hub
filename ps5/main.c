/* SPDX-License-Identifier: GPL-3.0-or-later
 * Copyright (c) 2026 ZYVORA contributors
 * Prototype compagnon ELF. Pas un PKG installable. */
#include <stdio.h>
#include <string.h>
#include <sys/stat.h>
#include <errno.h>

typedef struct { char reserved[45]; char message[3075]; } notify_request_t;
extern int sceKernelSendNotificationRequest(int, notify_request_t *, size_t, int);

int main(void) {
    if (mkdir("/data/zyvora", 0700) != 0 && errno != EEXIST) return 1;
    FILE *report = fopen("/data/zyvora/companion-status.json", "w");
    if (!report) return 2;
    if (fputs("{\"product\":\"ZYVORA\",\"version\":\"0.1.0\",\"type\":\"elf-prototype\",\"firmwareVerified\":false}\n", report) < 0) { fclose(report); return 3; }
    if (fclose(report) != 0) return 4;
    notify_request_t request;
    memset(&request, 0, sizeof(request));
    snprintf(request.message, sizeof(request.message), "ZYVORA companion 0.1.0 — rapport disponible dans /data/zyvora");
    return sceKernelSendNotificationRequest(0, &request, sizeof(request), 0);
}
