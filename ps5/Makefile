# SPDX-License-Identifier: GPL-3.0-or-later
ifndef PS5_PAYLOAD_SDK
$(error Set PS5_PAYLOAD_SDK to a public SDK installation)
endif
include $(PS5_PAYLOAD_SDK)/toolchain/prospero.mk
CFLAGS := -Wall -Wextra -Werror -O2
all: zyvora-companion.elf
zyvora-companion.elf: main.c
	$(CC) $(CFLAGS) -o $@ $<
