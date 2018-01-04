#include "soc/soc_ulp.h"
#include "soc/rtc_io_reg.h"
#include "soc/sens_reg.h"
#include "soc/rtc_cntl_reg.h"
//WRITE_RTC_REG(RTC_GPIO_OUT_W1TS_REG, RTC_GPIO_OUT_DATA_W1TS_S+12, 1, 1)//GP2 HIGH
//WRITE_RTC_REG(RTC_GPIO_OUT_W1TC_REG, RTC_GPIO_OUT_DATA_W1TC_S+12, 1, 1)//GP2 LOW
//WRITE_RTC_REG(RTC_CNTL_STATE0_REG, RTC_CNTL_ULP_CP_SLP_TIMER_EN, 0, 0)//Timer OFF





.bss
.global value
		value:.long 0

.text


	.global main
	main:
		WRITE_RTC_REG(RTC_GPIO_OUT_W1TS_REG, RTC_GPIO_OUT_DATA_W1TS_S+12, 1, 1)//GP2 HIGH

	jump loop
	
	loop:
		
		stage_rst
		100: wait 64000
		stage_inc 1
		jumps 100b, 2, lt
		WRITE_RTC_REG(RTC_GPIO_OUT_W1TC_REG, RTC_GPIO_OUT_DATA_W1TC_S+12, 1, 1)//GP2 LOW

		jump adcRead; 1:
		
		move r0, r2
		jumpr wkup, 600, lt


		jump loop

		jump adc
	adcRead:

		move r0, value
		move r1, 0
		move r2, 0

		stage_rst
		100:
			wait 40000
			wait 40000
			adc r1, 0, 5+1
			add r2, r2, r1
			stage_inc 1
			jumps 100b, 4, lt

		rsh r2, r2, 2
		st r2, r0, 0
		
		jump 1b

	wkup:
		wake
		WRITE_RTC_REG(RTC_CNTL_STATE0_REG, RTC_CNTL_ULP_CP_SLP_TIMER_EN, 0, 0)//Timer OFF
		halt